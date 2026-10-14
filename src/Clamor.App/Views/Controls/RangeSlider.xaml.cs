using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Clamor.App.Views.Controls;

/// <summary>
/// A slider with two thumbs selecting a <see cref="LowerValue"/>-<see cref="UpperValue"/> span
/// of <see cref="Minimum"/>-<see cref="Maximum"/>. WPF ships no range slider, and this only
/// needs drag, click-to-jump, and two-way binding, so it's drawn by hand on a Canvas.
/// </summary>
public partial class RangeSlider : UserControl
{
    private const double ThumbRadius = 7;

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(RangeSlider), new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(RangeSlider), new PropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty LowerValueProperty = DependencyProperty.Register(
        nameof(LowerValue), typeof(double), typeof(RangeSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty UpperValueProperty = DependencyProperty.Register(
        nameof(UpperValue), typeof(double), typeof(RangeSlider),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public RangeSlider()
    {
        InitializeComponent();
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double LowerValue
    {
        get => (double)GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    private double Span => Math.Max(Maximum - Minimum, double.Epsilon);

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RangeSlider)d).UpdatePositions();

    private void TrackCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePositions();

    private void LowerThumb_DragDelta(object sender, DragDeltaEventArgs e) =>
        LowerValue = Math.Clamp(LowerValue + ToValueDelta(e.HorizontalChange), Minimum, UpperValue);

    private void UpperThumb_DragDelta(object sender, DragDeltaEventArgs e) =>
        UpperValue = Math.Clamp(UpperValue + ToValueDelta(e.HorizontalChange), LowerValue, Maximum);

    /// <summary>Clicking the bare track jumps whichever thumb is closer to the click. Presses on a
    /// thumb itself never get here — Thumb handles them to start its drag.</summary>
    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        var width = TrackCanvas.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var x = Math.Clamp(e.GetPosition(TrackCanvas).X, 0, width);
        var value = Minimum + x / width * Span;
        if (Math.Abs(value - LowerValue) <= Math.Abs(value - UpperValue))
        {
            LowerValue = Math.Min(value, UpperValue);
        }
        else
        {
            UpperValue = Math.Max(value, LowerValue);
        }

        e.Handled = true;
    }

    private double ToValueDelta(double pixels) =>
        TrackCanvas.ActualWidth <= 0 ? 0 : pixels / TrackCanvas.ActualWidth * Span;

    private void UpdatePositions()
    {
        var width = TrackCanvas.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var lowerX = Math.Clamp((LowerValue - Minimum) / Span, 0, 1) * width;
        var upperX = Math.Clamp((UpperValue - Minimum) / Span, 0, 1) * width;

        Canvas.SetLeft(LowerThumb, lowerX - ThumbRadius);
        Canvas.SetLeft(UpperThumb, upperX - ThumbRadius);
        Canvas.SetLeft(SelectedRange, lowerX);
        SelectedRange.Width = Math.Max(0, upperX - lowerX);
    }
}
