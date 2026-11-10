using FluentValidation;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Expenses;
using SpendScope.Domain.ValueObjects;

namespace SpendScope.Features.Common;

/// <summary>Field rules from spec section 17, shared by every slice that accepts the field.</summary>
public static class ValidationRules
{
    public const int DisplayNameMin = 2;
    public const int DisplayNameMax = 50;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(e => EmailAddress.IsValid(e?.Trim()))
            .WithErrorCode("email_invalid")
            .WithMessage($"Enter a valid email address (at most {EmailAddress.MaxLength} characters).");

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(PasswordPolicy.IsSatisfied)
            .WithErrorCode("password_policy")
            .WithMessage(
                $"Password must be at least {PasswordPolicy.MinLength} characters with an uppercase letter, " +
                $"a lowercase letter, a digit, a symbol, and at least {PasswordPolicy.MinUniqueChars} different characters.");

    public static IRuleBuilderOptions<T, string> ValidDisplayName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(n => n is not null && n.Trim().Length is >= DisplayNameMin and <= DisplayNameMax && !n.Any(char.IsControl))
            .WithErrorCode("display_name_length")
            .WithMessage($"Display name must be {DisplayNameMin} to {DisplayNameMax} characters.");

    public static IRuleBuilderOptions<T, string> SupportedCurrency<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(SupportedCurrencies.IsSupported)
            .WithErrorCode("currency_unsupported")
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies.All)}.");

    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).WithErrorCode("amount_range").WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(ExpenseRules.MaxAmount).WithErrorCode("amount_range")
                .WithMessage($"Amount must be at most {ExpenseRules.MaxAmount:N2}.")
            .Must(a => decimal.Round(a, ExpenseRules.AmountScale) == a).WithErrorCode("amount_precision")
                .WithMessage("Amount can have at most two decimal places.");

    public static IRuleBuilderOptions<T, DateOnly> NotTooFarInFuture<T>(this IRuleBuilder<T, DateOnly> rule, Domain.Common.IClock clock) =>
        rule.Must(d => ExpenseRules.IsValidDate(d, clock.Today))
            .WithErrorCode("date_future")
            .WithMessage("Date cannot be more than 1 year in the future.");

    public static IRuleBuilderOptions<T, string?> ValidNote<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(n => n is null || n.Trim().Length <= ExpenseRules.NoteMaxLength)
                .WithErrorCode("note_length")
                .WithMessage($"Note must be at most {ExpenseRules.NoteMaxLength} characters.")
            .Must(n => n is null || !n.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
                .WithErrorCode("note_invalid")
                .WithMessage("Note contains invalid characters.");

    public static IRuleBuilderOptions<T, string> ValidCategoryName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(CategoryRules.IsValidName)
            .WithErrorCode("category_name_length")
            .WithMessage($"Category name must be 1 to {CategoryRules.NameMaxLength} characters.");

    public static IRuleBuilderOptions<T, string> ValidColorHex<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(HexColor.IsValid)
            .WithErrorCode("color_invalid")
            .WithMessage("Color must be in the form #RRGGBB.");

    public static IRuleBuilderOptions<T, int?> ValidPage<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(p => p is null or >= 1).WithErrorCode("page_invalid").WithMessage("Page must be 1 or greater.");

    public static IRuleBuilderOptions<T, int?> ValidPageSize<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(s => s is null or >= 1 and <= Pagination.MaxPageSize)
            .WithErrorCode("page_size_invalid")
            .WithMessage($"Page size must be between 1 and {Pagination.MaxPageSize}.");
}
