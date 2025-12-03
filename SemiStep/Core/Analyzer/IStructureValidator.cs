using Core.Entities;

namespace Core.Analyzer;

public interface IStructureValidator
{
	StructureResult Validate(Recipe recipe);
}
