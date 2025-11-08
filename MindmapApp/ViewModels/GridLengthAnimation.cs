using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace MindmapApp.ViewModels   // hoặc MindmapApp.Animations nếu bạn đổi xmlns
{
    /// <summary>
    /// Animation cho ColumnDefinition.Width/RowDefinition.Height (kiểu GridLength).
    /// </summary>
    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(GridLength),
                typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(0)));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(GridLength),
                typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(0)));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            // Lấy giá trị bắt đầu/kết thúc theo pixel (GridUnitType.Pixel).
            double fromVal = CoerceToPixels(From, defaultOriginValue);
            double toVal = CoerceToPixels(To, defaultDestinationValue);

            if (animationClock.CurrentProgress is double p)
            {
                double current = fromVal + (toVal - fromVal) * p;
                return new GridLength(current, GridUnitType.Pixel);
            }

            return new GridLength(fromVal, GridUnitType.Pixel);
        }

        private static double CoerceToPixels(GridLength length, object fallback)
        {
            if (length.GridUnitType == GridUnitType.Pixel) return length.Value;

            // Nếu From/To không chỉ định, lấy từ giá trị hiện tại của property.
            if (fallback is GridLength gl && gl.GridUnitType == GridUnitType.Pixel)
                return gl.Value;

            // Mặc định 0 nếu không xác định được
            return 0.0;
        }

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();
    }
}
