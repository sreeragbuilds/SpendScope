using SpendScope.Application.Common;

namespace SpendScope.Features.Categories;

/// <summary>Errors shared by the category slices.</summary>
public static class CategoryErrors
{
    public static readonly Error NotFound = Error.NotFound("category_not_found", "Category not found.");

    public static readonly Error ReadOnly = Error.Forbidden(
        Contracts.Common.ErrorCodes.SystemCategoryReadOnly, "System categories cannot be changed or deleted.");

    public static readonly Error NameTaken = Error.Conflict("category_name_taken", "You already have a category with this name.", "name");
}
