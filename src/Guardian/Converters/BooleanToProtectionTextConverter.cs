using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Guardian.Converters;

[ValueConversion(typeof(bool), typeof(object))]
internal sealed class BooleanToProtectionTextConverter : MarkupExtension, IValueConverter
{
    public string? TrueText { get; set; }

    public string? FalseText { get; set; }

    public override object ProvideValue(IServiceProvider provider) => this;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueText : FalseText;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}