  //\\   OmenMon WPF — Shared design system (palette, spacing, control factory)
 //  \\  Central theme with dark / light modes (default: follow system).
     //  https://omenmon.github.io/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SWM = System.Windows.Media;

namespace OmenMon.AppWpf {

    // Single source of truth for the app's visual language.
    public static class Theme {

#region Mode (dark / light / follow system)
        public enum ThemeMode { System, Light, Dark }

        public static ThemeMode Mode { get; private set; } = ThemeMode.System;

        // Effective darkness, resolving "System" via the Windows setting
        public static bool IsDark =>
            Mode == ThemeMode.Dark || (Mode == ThemeMode.System && SystemIsDark());

        public static string ModeName =>
            Mode == ThemeMode.System ? (IsDark ? "跟随系统（暗）" : "跟随系统（亮）") :
            Mode == ThemeMode.Light  ? "亮色" : "暗色";

        // System → Light → Dark → System …
        public static void CycleMode() {
            Mode = Mode == ThemeMode.System ? ThemeMode.Light :
                   Mode == ThemeMode.Light  ? ThemeMode.Dark  : ThemeMode.System;
        }

        // Windows personalization: AppsUseLightTheme == 0 → dark apps
        private static bool SystemIsDark() {
            try {
                using(var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    var v = key?.GetValue("AppsUseLightTheme");
                    if(v is int i) return i == 0;
                }
            } catch { }
            return true; // default to dark if unknown
        }
#endregion

#region Palette (mode-aware properties — call sites never change)
        public static SWM.Color BgWindow => IsDark ? Hex("#0D0D14") : Hex("#EEF0F6");
        public static SWM.Color BgMain   => IsDark ? Hex("#12121C") : Hex("#F5F6FA");
        public static SWM.Color BgSide   => IsDark ? Hex("#16161F") : Hex("#E7E9F2");
        public static SWM.Color BgCard   => IsDark ? Hex("#1B1B29") : Hex("#FFFFFF");
        public static SWM.Color BgCard2  => IsDark ? Hex("#252538") : Hex("#E4E6F0");
        public static SWM.Color BgHover  => IsDark ? Hex("#24243A") : Hex("#DFE3F0");
        public static SWM.Color BgActive => IsDark ? Hex("#1E2B4D") : Hex("#D5E3FF");

        public static SWM.Color BorderCol => IsDark ? Hex("#2A2A42") : Hex("#D5D8E6");

        public static SWM.Color FgPri  => IsDark ? Hex("#E6E6F2") : Hex("#1B1B2E");
        public static SWM.Color FgSec  => IsDark ? Hex("#9A9ABE") : Hex("#4E5375");
        public static SWM.Color FgMute => IsDark ? Hex("#5E5E80") : Hex("#8A8FAD");

        public static SWM.Color Blue   => IsDark ? Hex("#4E9EFF") : Hex("#2B6FE0");
        public static SWM.Color Green  => IsDark ? Hex("#3FE08A") : Hex("#0FA35E");
        public static SWM.Color Amber  => IsDark ? Hex("#FFB84E") : Hex("#C97E14");
        public static SWM.Color Red    => IsDark ? Hex("#FF5C5C") : Hex("#D93A3A");
        public static SWM.Color Violet => IsDark ? Hex("#A67EFF") : Hex("#7A4FE0");
#endregion

#region Spacing / Radius
        public const double PagePad  = 28;
        public const double CardPad  = 20;
        public const double CardGap  = 14;
        public const double Radius   = 14;
        public const double RadiusSm = 8;
#endregion

#region Brush helpers
        public static SolidColorBrush Br(SWM.Color c) => new SolidColorBrush(c);
        public static SolidColorBrush Br(string hex)  => new SolidColorBrush(Hex(hex));
        public static SWM.Color Hex(string hex) => (SWM.Color) SWM.ColorConverter.ConvertFromString(hex);

        public static SolidColorBrush Tint(SWM.Color c, byte alpha)
            => new SolidColorBrush(SWM.Color.FromArgb(alpha, c.R, c.G, c.B));

        public static SWM.Color TempColor(int t)
            => t >= 75 ? Red : t >= 60 ? Amber : Blue;
#endregion

#region Control factory
        public static Border Card(Thickness? margin = null) {
            return new Border {
                Background      = Br(BgCard),
                CornerRadius    = new CornerRadius(Radius),
                BorderBrush     = Br(BorderCol),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(CardPad),
                Margin          = margin ?? new Thickness(0)
            };
        }

        public static StackPanel PageHeader(string title, string subtitle) {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
            sp.Children.Add(new TextBlock {
                Text = title, FontSize = 24, FontWeight = FontWeights.Bold,
                Foreground = Br(FgPri), Margin = new Thickness(0, 0, 0, 4)
            });
            sp.Children.Add(new TextBlock {
                Text = subtitle, FontSize = 12.5, Foreground = Br(FgSec)
            });
            return sp;
        }

        public static TextBlock SectionTitle(string text, Thickness? margin = null) {
            return new TextBlock {
                Text = text, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = Br(FgPri), Margin = margin ?? new Thickness(0, 0, 0, 4)
            };
        }

        public static TextBlock Caption(string text, Thickness? margin = null) {
            return new TextBlock {
                Text = text, FontSize = 11.5, Foreground = Br(FgMute),
                TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0)
            };
        }

        public static Button PillButton(string text, SWM.Color accent, bool filled = false) {
            var btn = new Button {
                Padding = new Thickness(18, 9, 18, 9),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = filled ? Br("#FFFFFF") : Br(accent),
                Background = filled ? Br(accent) : Tint(accent, 40)
            };
            btn.Content = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium };
            var baseBg  = filled ? accent : SWM.Color.FromArgb(40, accent.R, accent.G, accent.B);
            var hoverBg = filled
                ? SWM.Color.FromArgb(255, (byte)Math.Min(accent.R + 22, 255), (byte)Math.Min(accent.G + 22, 255), (byte)Math.Min(accent.B + 22, 255))
                : SWM.Color.FromArgb(72, accent.R, accent.G, accent.B);
            btn.Template = RoundedButtonTemplate(RadiusSm);
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(hoverBg);
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(baseBg);
            return btn;
        }

        public static ControlTemplate RoundedButtonTemplate(double radius) {
            var t = new ControlTemplate(typeof(Button));
            var brd = new FrameworkElementFactory(typeof(Border));
            brd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            brd.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            brd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            brd.AppendChild(cp);
            t.VisualTree = brd;
            return t;
        }

        // Styled slider. Track parts (Decrease/Increase/Thumb) are Track
        // *properties*, which FrameworkElementFactory cannot populate —
        // building this template in code throws at Seal(). XAML handles
        // them natively, so the template is parsed from an XAML string.
        public static Slider FanSlider(SWM.Color accent) {
            string accentHex = accent.ToString();                    // #AARRGGBB
            string trackHex  = BgCard2.ToString();
            string strokeHex = IsDark ? "#40FFFFFF" : "#30000000";

            string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='Slider'>
  <Grid VerticalAlignment='Center' Background='Transparent' Height='22'>
    <Track x:Name='PART_Track'>
      <Track.DecreaseRepeatButton>
        <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>
          <RepeatButton.Template>
            <ControlTemplate TargetType='RepeatButton'>
              <Border Height='6' CornerRadius='3' Background='ACCENT'/>
            </ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command='Slider.IncreaseLarge' Focusable='False'>
          <RepeatButton.Template>
            <ControlTemplate TargetType='RepeatButton'>
              <Border Height='6' CornerRadius='3' Background='TRACKBG'/>
            </ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
      <Track.Thumb>
        <Thumb Width='18' Height='18' Focusable='False'>
          <Thumb.Template>
            <ControlTemplate TargetType='Thumb'>
              <Ellipse Fill='ACCENT' Stroke='STROKE' StrokeThickness='1'/>
            </ControlTemplate>
          </Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </Grid>
</ControlTemplate>"
                .Replace("ACCENT", accentHex)
                .Replace("TRACKBG", trackHex)
                .Replace("STROKE", strokeHex);

            var slider = new Slider {
                Minimum = 20, Maximum = 55, SmallChange = 1, LargeChange = 5,
                IsMoveToPointEnabled = true, Height = 22
            };
            slider.Template = (ControlTemplate) System.Windows.Markup.XamlReader.Parse(xaml);
            return slider;
        }

        public static ProgressBar Bar(SWM.Color accent, double height = 5) {
            return new ProgressBar {
                Maximum = 1, Height = height,
                Background = Br(BgCard2),
                Foreground = new SolidColorBrush(accent),
                BorderThickness = new Thickness(0)
            };
        }
#endregion
    }
}
