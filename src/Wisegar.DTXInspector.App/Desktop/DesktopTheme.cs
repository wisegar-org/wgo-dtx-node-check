using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Wisegar.DTXInspector.Desktop;

public static class DesktopTheme
{
    public static readonly IBrush Ink = Brush.Parse("#172B4D");
    public static readonly IBrush Muted = Brush.Parse("#526379");
    public static readonly IBrush Canvas = Brush.Parse("#F4F7FB");
    public static readonly IBrush Line = Brush.Parse("#DCE3ED");

    public static void Install(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Light;
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = Color.Parse("#DA291C") };
        app.Styles.Add(fluent);
        // Style the Fluent template state resources as well as the resting control.
        // Otherwise popup surfaces and disabled/hover states fall back to gray.
        var hover = Brush.Parse("#FFF1F0");
        var pressed = Brush.Parse("#FEE2DE");
        var accent = Brush.Parse("#DA291C");
        var disabled = Brush.Parse("#F1F4F8");
        var disabledInk = Brush.Parse("#8491A3");
        void Resources(IBrush brush, params string[] keys)
        {
            foreach (var key in keys) app.Resources[key] = brush;
        }
        Resources(Brushes.White, "ComboBoxDropDownBackground", "ComboBoxBackground", "ButtonBackground");
        Resources(Line, "ComboBoxDropDownBorderBrush", "ComboBoxBorderBrush", "ButtonBorderBrush",
            "ButtonBorderBrushDisabled", "AccentButtonBorderBrushDisabled", "ComboBoxBorderBrushDisabled");
        Resources(Ink, "ComboBoxForeground", "ComboBoxItemForeground", "ButtonForeground");
        Resources(hover, "ButtonBackgroundPointerOver", "ComboBoxBackgroundPointerOver",
            "ComboBoxItemBackgroundPointerOver", "ComboBoxItemBackgroundSelected");
        Resources(pressed, "ButtonBackgroundPressed", "ComboBoxBackgroundPressed",
            "ComboBoxItemBackgroundPressed", "ComboBoxItemBackgroundSelectedPointerOver",
            "ComboBoxItemBackgroundSelectedPressed");
        Resources(accent, "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed",
            "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed", "ComboBoxBackgroundBorderBrushFocused");
        Resources(Ink, "ComboBoxForegroundFocused", "ComboBoxForegroundFocusedPressed",
            "ComboBoxDropDownGlyphForegroundFocused", "ComboBoxDropDownGlyphForegroundFocusedPressed");
        Resources(Brush.Parse("#B32016"), "ButtonForegroundPointerOver", "ButtonForegroundPressed",
            "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundPressed", "ComboBoxItemForegroundSelected",
            "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxItemForegroundSelectedPressed");
        Resources(disabled, "ButtonBackgroundDisabled", "AccentButtonBackgroundDisabled", "ComboBoxBackgroundDisabled",
            "ComboBoxItemBackgroundSelectedDisabled");
        Resources(disabledInk, "ButtonForegroundDisabled", "AccentButtonForegroundDisabled", "ComboBoxForegroundDisabled",
            "ComboBoxDropDownGlyphForegroundDisabled", "ComboBoxItemForegroundDisabled", "ComboBoxItemForegroundSelectedDisabled");
        Resources(Brushes.Transparent, "ComboBoxItemBorderBrushPointerOver", "ComboBoxItemBorderBrushPressed",
            "ComboBoxItemBorderBrushSelected", "ComboBoxItemBorderBrushSelectedPointerOver", "ComboBoxItemBorderBrushSelectedPressed");
        app.Resources["ComboBoxDropdownBorderPadding"] = new Thickness(5);
        app.Resources["ComboBoxDropdownContentMargin"] = new Thickness(0);
        app.Resources["ComboBoxDropdownBorderThickness"] = new Thickness(1);
        app.Styles.Add(new Style(s => s.OfType<ComboBoxItem>()) { Setters = {
            new Setter(ComboBoxItem.MinHeightProperty, 38d),
            new Setter(ComboBoxItem.PaddingProperty, new Thickness(12, 9)),
            new Setter(ComboBoxItem.CornerRadiusProperty, new CornerRadius(5)),
            new Setter(ComboBoxItem.FontSizeProperty, 14d)
        } });
        // Flat surfaces, including template borders and native popup windows.
        app.Styles.Add(new Style(s => s.OfType<Border>()) { Setters = {
            new Setter(Border.BoxShadowProperty, default(BoxShadows))
        } });
        app.Styles.Add(new Style(s => s.OfType<Popup>()) { Setters = {
            new Setter(Popup.WindowManagerAddShadowHintProperty, false)
        } });
        app.Resources["MenuFlyoutPresenterBackground"] = Brushes.White;
        app.Resources["MenuFlyoutPresenterBorderBrush"] = Line;
        app.Resources["MenuFlyoutPresenterBorderThemeThickness"] = new Thickness(1);
        app.Resources["MenuFlyoutPresenterThemePadding"] = new Thickness(6);
        app.Resources["OverlayCornerRadius"] = new CornerRadius(8);
        app.Resources["ExpanderHeaderBorderBrush"] = Line;
        app.Resources["ExpanderContentBorderBrush"] = Line;
        app.Resources["ControlCornerRadius"] = new CornerRadius(6);
        app.Resources["MenuFlyoutItemForeground"] = Ink;
        app.Resources["MenuFlyoutItemBackgroundPointerOver"] = Brush.Parse("#FFF1F0");
        app.Resources["MenuFlyoutItemBackgroundPressed"] = Brush.Parse("#FEE2DE");
        app.Resources["MenuFlyoutItemForegroundPointerOver"] = Brush.Parse("#B32016");
        app.Resources["MenuFlyoutItemForegroundPressed"] = Brush.Parse("#B32016");
        app.Resources["MenuFlyoutSubItemChevronPointerOver"] = Brush.Parse("#B32016");
        app.Resources["MenuFlyoutSubItemChevronPressed"] = Brush.Parse("#B32016");
        app.Styles.Add(new Style(s => s.OfType<MenuItem>()) { Setters = {
            new Setter(MenuItem.MinHeightProperty, 38d),
            new Setter(MenuItem.PaddingProperty, new Thickness(12, 9)),
            new Setter(MenuItem.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(MenuItem.FontSizeProperty, 14d),
            new Setter(MenuItem.FontFamilyProperty, new FontFamily("Segoe UI"))
        } });
        using var iconStream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://WgoDtxInspector/Assets/app-icon.ico"));
        app.Styles.Add(new Style(s => s.OfType<Window>()) { Setters = {
            new Setter(Window.IconProperty, new WindowIcon(iconStream)),
            new Setter(Window.BackgroundProperty, Canvas),
            new Setter(Window.ForegroundProperty, Ink),
            new Setter(Window.FontFamilyProperty, new FontFamily("Segoe UI")),
            new Setter(Window.FontSizeProperty, 14d)
        } });
        app.Styles.Add(new Style(s => s.OfType<Button>()) { Setters = {
            new Setter(Button.MinHeightProperty, 40d),
            new Setter(Button.PaddingProperty, new Thickness(14, 9)),
            new Setter(Button.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(Button.FontWeightProperty, FontWeight.SemiBold)
        } });
        app.Styles.Add(new Style(s => s.OfType<Button>().Not(x => x.Class("accent"))) { Setters = {
            new Setter(Button.BackgroundProperty, Brushes.White),
            new Setter(Button.BorderBrushProperty, Line),
            new Setter(Button.BorderThicknessProperty, new Thickness(1))
        } });
        app.Styles.Add(new Style(s => s.OfType<ComboBox>()) { Setters = {
            new Setter(ComboBox.MinHeightProperty, 40d),
            new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(ComboBox.BorderBrushProperty, Line),
            new Setter(ComboBox.BackgroundProperty, Brushes.White)
        } });
        app.Styles.Add(new Style(s => s.OfType<TextBox>()) { Setters = {
            new Setter(TextBox.MinHeightProperty, 40d),
            new Setter(TextBox.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(TextBox.BorderBrushProperty, Line),
            new Setter(TextBox.BackgroundProperty, Brushes.White)
        } });
    }

    public static void ToolbarButton(Button button, string label, string geometry, bool primary = false)
    {
        button.Classes.Add(primary ? "accent" : "secondary");
        button.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
            new PathIcon { Data = Geometry.Parse(geometry), Width = 16, Height = 16 },
            new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
        } };
        Avalonia.Automation.AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
    }
}
