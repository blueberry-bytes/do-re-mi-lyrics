using System.Windows;
using System.Windows.Controls;

namespace Do_Re_Mi_Lyrics.Helper;

internal class TextBoxHelper : DependencyObject
{
    private const int CaretIndexPropertyDefault = -485609317;

    public static readonly DependencyProperty CaretIndexProperty = DependencyProperty.RegisterAttached("CaretIndex",
        typeof(int), typeof(TextBoxHelper),
        new FrameworkPropertyMetadata(CaretIndexPropertyDefault, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            CaretIndexChanged));

    public static void SetCaretIndex(DependencyObject dependencyObject, int i)
    {
        dependencyObject.SetValue(CaretIndexProperty, i);
    }

    public static int GetCaretIndex(DependencyObject dependencyObject)
    {
        return (int) dependencyObject.GetValue(CaretIndexProperty);
    }

    private static void CaretIndexChanged(DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not TextBox textBox || eventArgs.OldValue is not int oldValue ||
            eventArgs.NewValue is not int newValue)
        {
            return;
        }

        if (oldValue == CaretIndexPropertyDefault && newValue != CaretIndexPropertyDefault)
        {
            textBox.SelectionChanged += SelectionChangedForCaretIndex;
        }
        else if (oldValue != CaretIndexPropertyDefault && newValue == CaretIndexPropertyDefault)
        {
            textBox.SelectionChanged -= SelectionChangedForCaretIndex;
        }

        if (newValue != textBox.CaretIndex)
        {
            textBox.CaretIndex = newValue;
        }
    }

    private static void SelectionChangedForCaretIndex(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is TextBox textBox)
        {
            SetCaretIndex(textBox, textBox.CaretIndex);
        }
    }
}