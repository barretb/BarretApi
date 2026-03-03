using Microsoft.Extensions.Options;

namespace BarretApi.Api.Validation;

internal sealed class OptionsValidatorAdapter<TOptions> : IValidateOptions<TOptions>
	where TOptions : class
{
	private readonly Func<TOptions, string?> _validate;

	public OptionsValidatorAdapter(Func<TOptions, string?> validate)
	{
		_validate = validate;
	}

	public ValidateOptionsResult Validate(string? name, TOptions options)
	{
		var error = _validate(options);
		return error is null
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(error);
	}
}
