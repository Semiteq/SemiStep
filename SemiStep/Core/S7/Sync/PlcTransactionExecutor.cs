using FluentResults;

using S7.Protocol;
using S7.Serialization;

using Serilog;

using TypesShared.Core;
using TypesShared.Plc;

namespace S7.Sync;

internal sealed class PlcTransactionExecutor
{
	private readonly ArrayCodec _arrayCodec;
	private readonly RecipeConverter _converter;
	private readonly ExecutionStateCodec _executionCodec;
	private readonly PlcProtocolLayout _layout;
	private readonly ManagingAreaCodec _managingCodec;
	private readonly PlcProtocolSettings _protocolSettings;
	private readonly IS7Transport _transport;

	public PlcTransactionExecutor(
		IS7Transport transport,
		RecipeConverter converter,
		PlcConfiguration plcConfiguration)
	{
		_transport = transport;
		_converter = converter;
		_layout = plcConfiguration.Layout;
		_protocolSettings = plcConfiguration.ProtocolSettings;
		_arrayCodec = new ArrayCodec(_layout.IntDb, _layout.FloatDb, _layout.StringDb);
		_executionCodec = new ExecutionStateCodec(_layout.ExecutionDb);
		_managingCodec = new ManagingAreaCodec(_layout.ManagingDb);
	}

	public async Task<Result<bool>> IsRecipeActiveAsync(CancellationToken ct = default)
	{
		var result = await ReadExecutionStateAsync(ct);
		return result.Map(info => info.RecipeActive);
	}

	public Task<Result<PlcExecutionInfo>> ReadExecutionStateAsync(CancellationToken ct = default)
	{
		return ReadAndDecodeAsync(
			_layout.ExecutionDb.DbNumber,
			_layout.ExecutionDb.TotalSize,
			_executionCodec.Decode,
			ct);
	}

	public async Task<Result<PlcRecipeData>> ReadRecipeDataAsync(CancellationToken ct = default)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError("Not connected to PLC"));
		}

		try
		{
			var intHeaderBytes = await _transport.ReadBytesAsync(
				_layout.IntDb.DbNumber,
				0,
				_layout.IntDb.DataStartOffset,
				ct);
			var intCount = ArrayCodec.ReadArrayCurrentSize(intHeaderBytes, _layout.IntDb);

			var floatHeaderBytes = await _transport.ReadBytesAsync(
				_layout.FloatDb.DbNumber,
				0,
				_layout.FloatDb.DataStartOffset,
				ct);
			var floatCount = ArrayCodec.ReadArrayCurrentSize(floatHeaderBytes, _layout.FloatDb);

			var stringHeaderBytes = await _transport.ReadBytesAsync(
				_layout.StringDb.DbNumber,
				0,
				_layout.StringDb.DataStartOffset,
				ct);
			var stringCount = ArrayCodec.ReadArrayCurrentSize(stringHeaderBytes, _layout.StringDb);

			var intDataSize = _layout.IntDb.DataStartOffset + intCount * ProtocolConstants.IntElementSize;
			var floatDataSize = _layout.FloatDb.DataStartOffset + floatCount * ProtocolConstants.FloatElementSize;
			var stringDataSize = _layout.StringDb.DataStartOffset + stringCount * ProtocolConstants.WStringElementSize;

			var intData = await _transport.ReadBytesAsync(_layout.IntDb.DbNumber, 0, intDataSize, ct);
			var floatData = await _transport.ReadBytesAsync(_layout.FloatDb.DbNumber, 0, floatDataSize, ct);
			var stringData = await _transport.ReadBytesAsync(_layout.StringDb.DbNumber, 0, stringDataSize, ct);

			var intValues = _arrayCodec.DecodeIntArray(intData, intCount);
			var floatValues = _arrayCodec.DecodeFloatArray(floatData, floatCount);
			var stringValues = _arrayCodec.DecodeStringArray(stringData, stringCount);

			return Result.Ok(new PlcRecipeData(intValues, floatValues, stringValues, StepCount: intCount));
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return Result.Fail(ex.Message);
		}
	}

	public async Task<Result> WriteRecipeWithRetryAsync(Recipe recipe, CancellationToken ct = default)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError("Not connected to PLC"));
		}

		var dataResult = _converter.FromRecipe(recipe);
		if (dataResult.IsFailed)
		{
			return dataResult.ToResult();
		}

		var recipeData = dataResult.Value;

		for (var attempt = 1; attempt <= _protocolSettings.MaxRetryAttempts; attempt++)
		{
			var writeResult = await WriteRecipeDataAsync(recipeData, ct);
			if (writeResult.IsFailed)
			{
				return writeResult;
			}

			var verifyResult = await VerifyWriteAsync(recipeData, ct);
			if (verifyResult.IsFailed)
			{
				return verifyResult.ToResult();
			}

			if (verifyResult.Value)
			{
				Log.Information(
					"Recipe synced to PLC successfully ({StepCount} steps, attempt {Attempt})",
					recipe.StepCount,
					attempt);

				return Result.Ok();
			}

			Log.Warning(
				"Write verification failed on attempt {Attempt} of {MaxAttempts}",
				attempt,
				_protocolSettings.MaxRetryAttempts);
		}

		return Result.Fail(
			$"Recipe write verification failed after {_protocolSettings.MaxRetryAttempts} attempts");
	}

	public Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync(CancellationToken ct = default)
	{
		return ReadAndDecodeAsync(
			_layout.ManagingDb.DbNumber,
			_layout.ManagingDb.TotalSize,
			_managingCodec.Decode,
			ct);
	}

	public async Task<Result<Recipe>> ReadRecipeFromPlcAsync(CancellationToken ct = default)
	{
		var managingAreaResult = await ReadManagingAreaAsync(ct);
		if (managingAreaResult.IsFailed)
		{
			return managingAreaResult.ToResult<Recipe>();
		}

		if (!managingAreaResult.Value.Committed)
		{
			return Result.Fail("Recipe not committed on PLC");
		}

		var recipeDataResult = await ReadRecipeDataAsync(ct);
		if (recipeDataResult.IsFailed)
		{
			return recipeDataResult.ToResult<Recipe>();
		}

		return _converter.ToRecipe(recipeDataResult.Value);
	}

	private async Task<Result<T>> ReadAndDecodeAsync<T>(
		int dbNumber, int size, Func<byte[], Result<T>> decode, CancellationToken ct)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError("Not connected to PLC"));
		}

		try
		{
			var bytes = await _transport.ReadBytesAsync(dbNumber, 0, size, ct);
			return decode(bytes);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return Result.Fail(ex.Message);
		}
	}

	private async Task<Result> WriteRecipeDataAsync(PlcRecipeData data, CancellationToken ct)
	{
		try
		{
			var writeManagingResult = await WriteManagingAreaAsync(
				new ManagingAreaPcData(Committed: false, RecipeLines: 0), ct);
			if (writeManagingResult.IsFailed)
			{
				return writeManagingResult;
			}

			var intBytes = _arrayCodec.EncodeIntArray(data.IntValues);
			var floatBytes = _arrayCodec.EncodeFloatArray(data.FloatValues);
			var stringBytes = _arrayCodec.EncodeStringArray(data.StringValues);

			await _transport.WriteBytesAsync(_layout.IntDb.DbNumber, 0, intBytes, ct);
			await _transport.WriteBytesAsync(_layout.FloatDb.DbNumber, 0, floatBytes, ct);
			await _transport.WriteBytesAsync(_layout.StringDb.DbNumber, 0, stringBytes, ct);

			var writeLinesResult = await WriteManagingAreaAsync(
				new ManagingAreaPcData(Committed: false, RecipeLines: data.StepCount), ct);
			if (writeLinesResult.IsFailed)
			{
				return writeLinesResult;
			}

			return await WriteManagingAreaAsync(
				new ManagingAreaPcData(Committed: true, RecipeLines: data.StepCount), ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return Result.Fail(ex.Message);
		}
	}

	private async Task<Result<bool>> VerifyWriteAsync(PlcRecipeData expected, CancellationToken ct)
	{
		var actualResult = await ReadRecipeDataAsync(ct);
		if (actualResult.IsFailed)
		{
			return actualResult.ToResult<bool>();
		}

		return Result.Ok(PlcRecipeDataComparer.DataMatchesExpected(actualResult.Value, expected));
	}

	private async Task<Result> WriteManagingAreaAsync(ManagingAreaPcData data, CancellationToken ct)
	{
		try
		{
			var bytes = _managingCodec.EncodePcData(data);
			await _transport.WriteBytesAsync(_layout.ManagingDb.DbNumber, 0, bytes, ct);
			return Result.Ok();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return Result.Fail(ex.Message);
		}
	}
}
