using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PingByDaylight.Presentation.Components;

/// <summary>
/// Custom loading spinner control with rotating animation
/// </summary>
public class LoadingSpinner : Control
{
    static LoadingSpinner()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LoadingSpinner), new FrameworkPropertyMetadata(typeof(LoadingSpinner)));
    }

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(LoadingSpinner), 
            new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private RotateTransform? _rotateTransform;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        
        if (GetTemplateChild("PART_RotateTransform") is RotateTransform transform)
        {
            _rotateTransform = transform;
            UpdateAnimation();
        }
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingSpinner spinner)
        {
            spinner.UpdateAnimation();
        }
    }

    private void UpdateAnimation()
    {
        if (_rotateTransform == null)
            return;

        _rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);

        if (IsActive)
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(800),
                RepeatBehavior = RepeatBehavior.Forever,
                IsCumulative = true
            };
            
            _rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
        else
        {
            _rotateTransform.Angle = 0;
        }
    }
}
