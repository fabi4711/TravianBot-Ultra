using System.Globalization;
using System.Reflection;

namespace System.Windows.Media
{
    public struct Color : IEquatable<Color>
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public static Color FromRgb(byte r, byte g, byte b) => new() { A = 255, R = r, G = g, B = b };

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new() { A = a, R = r, G = g, B = b };

        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

        public override bool Equals(object? obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(A, R, G, B);

        public static bool operator ==(Color left, Color right) => left.Equals(right);

        public static bool operator !=(Color left, Color right) => !left.Equals(right);

        /// <summary>WPF's format: #AARRGGBB.</summary>
        public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <summary>CSS color for the web UI.</summary>
        public string ToCss() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"rgba({R},{G},{B},{(A / 255d).ToString("0.###", CultureInfo.InvariantCulture)})";
    }

    public static class Colors
    {
        public static Color Transparent => Color.FromArgb(0, 255, 255, 255);
        public static Color Black => Color.FromRgb(0, 0, 0);
        public static Color White => Color.FromRgb(255, 255, 255);
        public static Color Red => Color.FromRgb(255, 0, 0);
        public static Color Green => Color.FromRgb(0, 128, 0);
        public static Color LimeGreen => Color.FromRgb(50, 205, 50);
        public static Color Lime => Color.FromRgb(0, 255, 0);
        public static Color Blue => Color.FromRgb(0, 0, 255);
        public static Color Yellow => Color.FromRgb(255, 255, 0);
        public static Color Orange => Color.FromRgb(255, 165, 0);
        public static Color OrangeRed => Color.FromRgb(255, 69, 0);
        public static Color Gold => Color.FromRgb(255, 215, 0);
        public static Color Gray => Color.FromRgb(128, 128, 128);
        public static Color DarkGray => Color.FromRgb(169, 169, 169);
        public static Color LightGray => Color.FromRgb(211, 211, 211);
        public static Color DimGray => Color.FromRgb(105, 105, 105);
        public static Color Silver => Color.FromRgb(192, 192, 192);
        public static Color Magenta => Color.FromRgb(255, 0, 255);
        public static Color Cyan => Color.FromRgb(0, 255, 255);
        public static Color Purple => Color.FromRgb(128, 0, 128);
        public static Color Brown => Color.FromRgb(165, 42, 42);
        public static Color DarkRed => Color.FromRgb(139, 0, 0);
        public static Color DarkGreen => Color.FromRgb(0, 100, 0);
        public static Color DarkOrange => Color.FromRgb(255, 140, 0);
        public static Color SteelBlue => Color.FromRgb(70, 130, 180);
        public static Color DodgerBlue => Color.FromRgb(30, 144, 255);
        public static Color Goldenrod => Color.FromRgb(218, 165, 32);
        public static Color Tomato => Color.FromRgb(255, 99, 71);
        public static Color Crimson => Color.FromRgb(220, 20, 60);
        public static Color WhiteSmoke => Color.FromRgb(245, 245, 245);
        public static Color Gainsboro => Color.FromRgb(220, 220, 220);
        public static Color SlateGray => Color.FromRgb(112, 128, 144);
        public static Color LightGreen => Color.FromRgb(144, 238, 144);
        public static Color LightBlue => Color.FromRgb(173, 216, 230);
        public static Color Khaki => Color.FromRgb(240, 230, 140);
        public static Color Coral => Color.FromRgb(255, 127, 80);

        internal static bool TryGet(string name, out Color color)
        {
            var property = typeof(Colors).GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (property?.GetValue(null) is Color value)
            {
                color = value;
                return true;
            }

            color = default;
            return false;
        }
    }

    public abstract class Brush : Freezable
    {
        public double Opacity { get => Get(1d); set => Set(value); }

        public new Brush Clone() => (Brush)MemberwiseClone();

        /// <summary>CSS representation for the web UI, or null when the brush has no flat color.</summary>
        public virtual string? ToCss() => null;
    }

    public sealed class SolidColorBrush : Brush
    {
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(Color), typeof(SolidColorBrush));

        public SolidColorBrush()
        {
        }

        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get => Get(default(Color)); set => Set(value); }

        public override string? ToCss() => Color.ToCss();

        public override string ToString() => Color.ToString();
    }

    public sealed class LinearGradientBrush : Brush
    {
        public LinearGradientBrush()
        {
        }

        public LinearGradientBrush(Color startColor, Color endColor, double angle)
        {
            GradientStops.Add(new GradientStop(startColor, 0));
            GradientStops.Add(new GradientStop(endColor, 1));
        }

        public GradientStopCollection GradientStops { get; } = new();
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }

        public override string? ToCss() => GradientStops.Count > 0 ? GradientStops[0].Color.ToCss() : null;
    }

    public sealed class GradientStop
    {
        public GradientStop()
        {
        }

        public GradientStop(Color color, double offset)
        {
            Color = color;
            Offset = offset;
        }

        public Color Color { get; set; }
        public double Offset { get; set; }
    }

    public sealed class GradientStopCollection : List<GradientStop>
    {
    }

    public sealed class ImageBrush : Brush
    {
        public ImageBrush()
        {
        }

        public ImageBrush(ImageSource source)
        {
            ImageSource = source;
        }

        public ImageSource? ImageSource { get; set; }
        public Stretch Stretch { get; set; }
    }

    public sealed class VisualBrush : Brush
    {
    }

    public static class Brushes
    {
        public static SolidColorBrush Transparent { get; } = Frozen(Colors.Transparent);
        public static SolidColorBrush Black { get; } = Frozen(Colors.Black);
        public static SolidColorBrush White { get; } = Frozen(Colors.White);
        public static SolidColorBrush Red { get; } = Frozen(Colors.Red);
        public static SolidColorBrush Green { get; } = Frozen(Colors.Green);
        public static SolidColorBrush LimeGreen { get; } = Frozen(Colors.LimeGreen);
        public static SolidColorBrush Blue { get; } = Frozen(Colors.Blue);
        public static SolidColorBrush Yellow { get; } = Frozen(Colors.Yellow);
        public static SolidColorBrush Orange { get; } = Frozen(Colors.Orange);
        public static SolidColorBrush OrangeRed { get; } = Frozen(Colors.OrangeRed);
        public static SolidColorBrush Gold { get; } = Frozen(Colors.Gold);
        public static SolidColorBrush Gray { get; } = Frozen(Colors.Gray);
        public static SolidColorBrush DarkGray { get; } = Frozen(Colors.DarkGray);
        public static SolidColorBrush LightGray { get; } = Frozen(Colors.LightGray);
        public static SolidColorBrush DimGray { get; } = Frozen(Colors.DimGray);
        public static SolidColorBrush Silver { get; } = Frozen(Colors.Silver);
        public static SolidColorBrush Magenta { get; } = Frozen(Colors.Magenta);
        public static SolidColorBrush DarkRed { get; } = Frozen(Colors.DarkRed);
        public static SolidColorBrush DarkGreen { get; } = Frozen(Colors.DarkGreen);
        public static SolidColorBrush DarkOrange { get; } = Frozen(Colors.DarkOrange);
        public static SolidColorBrush SteelBlue { get; } = Frozen(Colors.SteelBlue);
        public static SolidColorBrush DodgerBlue { get; } = Frozen(Colors.DodgerBlue);
        public static SolidColorBrush Goldenrod { get; } = Frozen(Colors.Goldenrod);
        public static SolidColorBrush Tomato { get; } = Frozen(Colors.Tomato);
        public static SolidColorBrush Crimson { get; } = Frozen(Colors.Crimson);
        public static SolidColorBrush WhiteSmoke { get; } = Frozen(Colors.WhiteSmoke);
        public static SolidColorBrush Gainsboro { get; } = Frozen(Colors.Gainsboro);
        public static SolidColorBrush SlateGray { get; } = Frozen(Colors.SlateGray);
        public static SolidColorBrush LightGreen { get; } = Frozen(Colors.LightGreen);
        public static SolidColorBrush Coral { get; } = Frozen(Colors.Coral);
        public static SolidColorBrush SeaGreen { get; } = Frozen(Color.FromRgb(46, 139, 87));
        public static SolidColorBrush ForestGreen { get; } = Frozen(Color.FromRgb(34, 139, 34));
        public static SolidColorBrush Firebrick { get; } = Frozen(Color.FromRgb(178, 34, 34));
        public static SolidColorBrush Navy { get; } = Frozen(Color.FromRgb(0, 0, 128));
        public static SolidColorBrush Teal { get; } = Frozen(Color.FromRgb(0, 128, 128));
        public static SolidColorBrush Purple { get; } = Frozen(Colors.Purple);
        public static SolidColorBrush Brown { get; } = Frozen(Colors.Brown);
        public static SolidColorBrush Cyan { get; } = Frozen(Colors.Cyan);

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    public sealed class BrushConverter
    {
        public object? ConvertFrom(object value) => value is string text ? ConvertFromString(text) : null;

        public object? ConvertFromString(string value)
            => ColorConverter.TryParse(value, out var color) ? new SolidColorBrush(color) : null;

        public object? ConvertFromInvariantString(string value) => ConvertFromString(value);
    }

    public sealed class ColorConverter
    {
        public static object ConvertFromString(string value)
            => TryParse(value, out var color) ? color : throw new FormatException($"Token is not valid: '{value}'.");

        internal static bool TryParse(string value, out Color color)
        {
            value = value.Trim();
            if (value.StartsWith('#'))
            {
                var hex = value[1..];
                if (hex.Length is 3 or 4)
                {
                    hex = string.Concat(hex.Select(ch => new string(ch, 2)));
                }

                if (hex.Length == 6)
                {
                    hex = "FF" + hex;
                }

                if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                {
                    color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
                    return true;
                }

                color = default;
                return false;
            }

            return Colors.TryGet(value, out color);
        }
    }

    public abstract class ImageSource : Freezable
    {
        public double Width => 0;
        public double Height => 0;
    }

    public class FontFamily
    {
        public FontFamily()
        {
        }

        public FontFamily(string familyName)
        {
            Source = familyName;
        }

        public string? Source { get; }

        public override string ToString() => Source ?? string.Empty;
    }

    public enum Stretch
    {
        None,
        Fill,
        Uniform,
        UniformToFill,
    }

    public abstract class GeneralTransform : Freezable
    {
    }

    public abstract class Transform : GeneralTransform
    {
        public static Transform Identity { get; } = new MatrixTransform();
    }

    public sealed class MatrixTransform : Transform
    {
    }

    public sealed class RotateTransform : Transform
    {
        public RotateTransform()
        {
        }

        public RotateTransform(double angle)
        {
            Angle = angle;
        }

        public double Angle { get => Get(0d); set => Set(value); }
        public double CenterX { get => Get(0d); set => Set(value); }
        public double CenterY { get => Get(0d); set => Set(value); }
    }

    public sealed class ScaleTransform : Transform
    {
        public ScaleTransform()
        {
        }

        public ScaleTransform(double scaleX, double scaleY)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
        }

        public double ScaleX { get => Get(1d); set => Set(value); }
        public double ScaleY { get => Get(1d); set => Set(value); }
    }

    public sealed class TranslateTransform : Transform
    {
        public TranslateTransform()
        {
        }

        public TranslateTransform(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get => Get(0d); set => Set(value); }
        public double Y { get => Get(0d); set => Set(value); }
    }

    public sealed class TransformGroup : Transform
    {
        public List<Transform> Children { get; } = new();
    }

    public abstract class Geometry : Freezable
    {
        public static Geometry Parse(string source) => new PathGeometry();
    }

    public sealed class PathGeometry : Geometry
    {
        public PathFigureCollection Figures { get; set; } = new();
    }

    public sealed class PathFigureCollection : List<PathFigure>
    {
    }

    public sealed class PathFigure : Freezable
    {
        public PathFigure()
        {
        }

        public PathFigure(Point start, IEnumerable<PathSegment> segments, bool closed)
        {
            StartPoint = start;
            Segments.AddRange(segments);
            IsClosed = closed;
        }

        public Point StartPoint { get; set; }
        public bool IsClosed { get; set; }
        public bool IsFilled { get; set; } = true;
        public PathSegmentCollection Segments { get; set; } = new();
    }

    public sealed class PathSegmentCollection : List<PathSegment>
    {
    }

    public abstract class PathSegment : Freezable
    {
        public bool IsStroked { get; set; } = true;
    }

    public sealed class LineSegment : PathSegment
    {
        public LineSegment()
        {
        }

        public LineSegment(Point point, bool isStroked)
        {
            Point = point;
            IsStroked = isStroked;
        }

        public Point Point { get; set; }
    }

    public sealed class BezierSegment : PathSegment
    {
        public BezierSegment()
        {
        }

        public BezierSegment(Point point1, Point point2, Point point3, bool isStroked)
        {
            Point1 = point1;
            Point2 = point2;
            Point3 = point3;
            IsStroked = isStroked;
        }

        public Point Point1 { get; set; }
        public Point Point2 { get; set; }
        public Point Point3 { get; set; }
    }

    public sealed class PolyLineSegment : PathSegment
    {
        public PointCollection Points { get; set; } = new();
    }

    public enum PenLineJoin
    {
        Miter,
        Bevel,
        Round,
    }

    public sealed class CompositionTarget
    {
        public static event EventHandler? Rendering;
    }

    public sealed class RectangleGeometry : Geometry
    {
        public RectangleGeometry()
        {
        }

        public RectangleGeometry(Rect rect)
        {
        }
    }

    public sealed class EllipseGeometry : Geometry
    {
    }

    public enum PenLineCap
    {
        Flat,
        Square,
        Round,
        Triangle,
    }

    public sealed class DoubleCollection : List<double>
    {
    }

    public sealed class PointCollection : List<Point>
    {
    }

    public static class VisualTreeHelper
    {
        public static DependencyObject? GetParent(DependencyObject reference) => (reference as UIElement)?.HeadlessParent;

        public static int GetChildrenCount(DependencyObject reference) => Headless.HeadlessXaml.GetVisualChildren(reference).Count;

        public static DependencyObject GetChild(DependencyObject reference, int childIndex)
            => Headless.HeadlessXaml.GetVisualChildren(reference)[childIndex];

        public static Rect GetDescendantBounds(Visual reference) => default;

        public static HitTestResult? HitTest(Visual reference, Point point) => null;
    }

    public class HitTestResult
    {
        public DependencyObject? VisualHit => null;
    }

    public static class RenderOptions
    {
        public static void SetBitmapScalingMode(DependencyObject target, BitmapScalingMode mode)
        {
        }
    }

    public enum BitmapScalingMode
    {
        Unspecified,
        LowQuality,
        HighQuality,
        NearestNeighbor,
    }

    public static class TextOptions
    {
        public static void SetTextFormattingMode(DependencyObject element, TextFormattingMode value)
        {
        }
    }

    public enum TextFormattingMode
    {
        Ideal,
        Display,
    }

    public sealed class Typeface
    {
        public Typeface(string typefaceName)
        {
        }
    }

    public sealed class FormattedText
    {
        public double Width => 0;
        public double Height => 0;
    }

    public static class Fonts
    {
    }
}

namespace System.Windows.Media.Effects
{
    public abstract class Effect : Freezable
    {
    }

    public sealed class DropShadowEffect : Effect
    {
        public Color Color { get; set; }
        public double BlurRadius { get; set; }
        public double ShadowDepth { get; set; }
        public double Opacity { get; set; }
        public double Direction { get; set; }
    }

    public sealed class BlurEffect : Effect
    {
        public double Radius { get; set; }
    }
}

namespace System.Windows.Media.Imaging
{
    public class BitmapSource : ImageSource
    {
        public int PixelWidth => 0;
        public int PixelHeight => 0;
    }

    public sealed class BitmapImage : BitmapSource
    {
        public BitmapImage()
        {
        }

        public BitmapImage(Uri uriSource)
        {
            UriSource = uriSource;
        }

        public Uri? UriSource { get; set; }
        public BitmapCacheOption CacheOption { get; set; }
        public System.IO.Stream? StreamSource { get; set; }
        public int DecodePixelWidth { get; set; }

        public void BeginInit()
        {
        }

        public void EndInit()
        {
        }
    }

    public enum BitmapCacheOption
    {
        Default,
        OnDemand,
        OnLoad,
        None,
    }

    public sealed class BitmapFrame : BitmapSource
    {
        public static BitmapFrame Create(Uri bitmapUri) => new();
    }

    public sealed class RenderTargetBitmap : BitmapSource
    {
        public RenderTargetBitmap(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat)
        {
        }

        public void Render(Visual visual)
        {
        }
    }

    public readonly struct PixelFormat
    {
    }

    public abstract class BitmapEncoder
    {
        public IList<BitmapFrame> Frames { get; } = new List<BitmapFrame>();

        public void Save(System.IO.Stream stream)
        {
        }
    }

    public sealed class PngBitmapEncoder : BitmapEncoder
    {
    }
}

namespace System.Windows.Media.Animation
{
    public sealed class Storyboard : Timeline
    {
        public List<Timeline> Children { get; } = new();

        public void Begin()
        {
        }

        public void Begin(FrameworkElement containingObject)
        {
        }

        public void Begin(FrameworkElement containingObject, bool isControllable)
        {
        }

        public void Stop()
        {
        }

        public void Stop(FrameworkElement containingObject)
        {
        }

        public static void SetTargetProperty(DependencyObject element, PropertyPath path)
        {
        }

        public static void SetTarget(DependencyObject element, DependencyObject value)
        {
        }

        public static void SetTargetName(DependencyObject element, string name)
        {
        }
    }

    public abstract class Timeline : Freezable
    {
        public Duration Duration { get; set; }
        public RepeatBehavior RepeatBehavior { get; set; }
        public bool AutoReverse { get; set; }
        public TimeSpan? BeginTime { get; set; }

        public event EventHandler? Completed;
    }

    public sealed class DoubleAnimation : Timeline
    {
        public DoubleAnimation()
        {
        }

        public DoubleAnimation(double toValue, Duration duration)
        {
            To = toValue;
            Duration = duration;
        }

        public DoubleAnimation(double fromValue, double toValue, Duration duration)
        {
            From = fromValue;
            To = toValue;
            Duration = duration;
        }

        public double? From { get; set; }
        public double? To { get; set; }
        public IEasingFunction? EasingFunction { get; set; }
    }

    public sealed class ColorAnimation : Timeline
    {
        public Color? From { get; set; }
        public Color? To { get; set; }
        public IEasingFunction? EasingFunction { get; set; }
    }

    public sealed class SineEase : IEasingFunction
    {
        public EasingMode EasingMode { get; set; }
    }

    public interface IEasingFunction
    {
    }

    public sealed class QuadraticEase : IEasingFunction
    {
        public EasingMode EasingMode { get; set; }
    }

    public sealed class CubicEase : IEasingFunction
    {
        public EasingMode EasingMode { get; set; }
    }

    public enum EasingMode
    {
        EaseIn,
        EaseOut,
        EaseInOut,
    }

    public readonly struct RepeatBehavior
    {
        public static RepeatBehavior Forever => default;
    }

    public sealed class BeginStoryboard : TriggerAction
    {
        public Storyboard? Storyboard { get; set; }
    }

    public abstract class TriggerAction : DependencyObject
    {
    }
}

namespace System.Windows
{
    public readonly struct Duration
    {
        public Duration(TimeSpan timeSpan)
        {
            TimeSpan = timeSpan;
        }

        public TimeSpan TimeSpan { get; }
        public bool HasTimeSpan => true;
        public static Duration Automatic => default;
        public static Duration Forever => default;

        public static implicit operator Duration(TimeSpan timeSpan) => new(timeSpan);
    }

    public sealed class PropertyPath
    {
        public PropertyPath(string path, params object[] pathParameters)
        {
            Path = path;
        }

        public PropertyPath(object parameter)
        {
            Path = parameter.ToString() ?? string.Empty;
        }

        public string Path { get; }
    }

    public class EventTrigger : TriggerBase
    {
        public RoutedEvent? RoutedEvent { get; set; }
        public List<Media.Animation.TriggerAction> Actions { get; } = new();
    }
}
