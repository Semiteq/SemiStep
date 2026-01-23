using Recipe.Entities;

namespace Recipe.Validation;

public abstract record ValidationError(ColumnId Column, string Message);

public sealed record TypeMismatchError(ColumnId Column, Type Expected, Type Actual)
	: ValidationError(Column, $"Expected type {Expected.Name}, got {Actual.Name}");

public sealed record ValueTooLowError(ColumnId Column, object Minimum, object Actual)
	: ValidationError(Column, $"Value {Actual} is below minimum {Minimum}");

public sealed record ValueTooHighError(ColumnId Column, object Maximum, object Actual)
	: ValidationError(Column, $"Value {Actual} is above maximum {Maximum}");

public sealed record StringTooLongError(ColumnId Column, int MaxLength, int ActualLength)
	: ValidationError(Column, $"String length {ActualLength} exceeds maximum {MaxLength}");

public sealed record UnknownPropertyError(ColumnId Column)
	: ValidationError(Column, $"Unknown property: {Column}");
