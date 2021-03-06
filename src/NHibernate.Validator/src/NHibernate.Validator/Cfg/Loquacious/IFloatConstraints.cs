namespace NHibernate.Validator.Cfg.Loquacious
{
	public interface IFloatConstraints
	{
		IRuleArgsOptions Digits(int integerDigits);
		IRuleArgsOptions Digits(int integerDigits, int fractionalDigits);
		IRuleArgsOptions LessThanOrEqualTo(long maxValue);
		IRuleArgsOptions GreaterThanOrEqualTo(long minValue);
		IRuleArgsOptions IncludedBetween(long minValue, long maxValue);
	}
}