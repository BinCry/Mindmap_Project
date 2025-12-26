using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace MindmapApp.ViewModels;

// Class này hỗ trợ Animation cho GridLength (dùng để ẩn/hiện Sidebar mượt mà)
public class GridLengthAnimation : AnimationTimeline
{
    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }

    // Dependency Property cho giá trị bắt đầu (From)
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register("From", typeof(GridLength),
        typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    // Dependency Property cho giá trị kết thúc (To)
    public static readonly DependencyProperty ToProperty = DependencyProperty.Register("To", typeof(GridLength),
        typeof(GridLengthAnimation));

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    // Hàm tính toán giá trị hiện tại của Animation
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue,
        AnimationClock animationClock)
    {
        double fromVal = ((GridLength)GetValue(FromProperty)).Value;
        double toVal = ((GridLength)GetValue(ToProperty)).Value;

        // Xử lý tính toán nội suy tuyến tính (Linear Interpolation)
        if (fromVal > toVal)
        {
            return new GridLength((1 - animationClock.CurrentProgress.Value) * (fromVal - toVal) + toVal,
                GridUnitType.Pixel);
        }

        return new GridLength(animationClock.CurrentProgress.Value * (toVal - fromVal) + fromVal, GridUnitType.Pixel);
    }
}