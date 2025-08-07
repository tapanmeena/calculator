using Windows.UI.Xaml.Input;
using Windows.Foundation;
using Windows.UI.Popups;
using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using CalculatorApp.ViewModel.Common;

using Windows.ApplicationModel.Core;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace CalculatorApp
{
    public sealed partial class TitleBar : UserControl
        private DateTime _lastAppIconTap = DateTime.MinValue;
        private const int DoubleTapMilliseconds = 400;
        // Handle single and double click on AppIcon
        private void AppIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Single tap: show system menu
            ShowSystemMenu();
        }

        private void AppIcon_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // Double tap: close window
            CloseWindow();
        }

        private void AppIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Prevent default to avoid event bubbling
            e.Handled = true;
        }

        private void ShowSystemMenu()
        {
            // Get the window handle (HWND) and show the system menu at the app icon location
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Window.Current.CoreWindow);
            if (hwnd == IntPtr.Zero)
                return;

            // Get the position of the AppIcon relative to the screen
            var transform = AppIcon.TransformToVisual(Window.Current.Content as UIElement);
            var point = transform.TransformPoint(new Point(0, AppIcon.ActualHeight));
            var screenPoint = AppIcon.PointToScreen(point);

            // Show the system menu using Win32 API
            NativeMethods.ShowSystemMenu(hwnd, (int)screenPoint.X, (int)screenPoint.Y);
        }

        private void CloseWindow()
        {
            // Close the window using CoreApplication
            Windows.ApplicationModel.Core.CoreApplication.Exit();
        }

        // Native interop for system menu
        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

            public const uint TPM_LEFTALIGN = 0x0000;
            public const uint TPM_RETURNCMD = 0x0100;
            public const uint WM_SYSCOMMAND = 0x0112;

            public static void ShowSystemMenu(IntPtr hwnd, int x, int y)
            {
                var hMenu = GetSystemMenu(hwnd, false);
                int cmd = TrackPopupMenuEx(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, x, y, hwnd, IntPtr.Zero);
                if (cmd != 0)
                {
                    PostMessage(hwnd, WM_SYSCOMMAND, (IntPtr)cmd, IntPtr.Zero);
                }
            }
        }
    {
        public TitleBar()
        {
            m_coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            m_uiSettings = new UISettings();
            m_accessibilitySettings = new AccessibilitySettings();
            InitializeComponent();

            m_coreTitleBar.ExtendViewIntoTitleBar = true;
            Window.Current.SetTitleBar(BackgroundElement);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
#if IS_STORE_BUILD
            AppName.Text = AppResourceProvider.GetInstance().GetResourceString("AppName");

#else
            AppName.Text = AppResourceProvider.GetInstance().GetResourceString("DevAppName");
#endif
        }

        public bool IsAlwaysOnTopMode
        {
            get => (bool)GetValue(IsAlwaysOnTopModeProperty);
            set => SetValue(IsAlwaysOnTopModeProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsAlwaysOnTopMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsAlwaysOnTopModeProperty =
            DependencyProperty.Register(nameof(IsAlwaysOnTopMode), typeof(bool), typeof(TitleBar), new PropertyMetadata(default(bool), (sender, args) =>
            {
                var self = (TitleBar)sender;
                self.OnIsAlwaysOnTopModePropertyChanged((bool)args.OldValue, (bool)args.NewValue);
            }));

        public event Windows.UI.Xaml.RoutedEventHandler AlwaysOnTopClick;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Register events
            m_coreTitleBar.IsVisibleChanged += CoreTitleBarIsVisibleChanged;
            m_coreTitleBar.LayoutMetricsChanged += CoreTitleBarLayoutMetricsChanged;

            m_uiSettings.ColorValuesChanged += ColorValuesChanged;
            m_accessibilitySettings.HighContrastChanged += OnHighContrastChanged;
            Window.Current.Activated += OnWindowActivated;

            // Register RequestedTheme changed callback to update title bar system button colors.
            m_rootFrameRequestedThemeCallbackToken =
                Utils.ThemeHelper.RegisterAppThemeChangedCallback(RootFrame_RequestedThemeChanged);

            // Set properties
            SetTitleBarControlColors();
            SetTitleBarHeightAndPadding();

            // As of Windows 10 1903: when an app runs on a PC (without Tablet mode activated)
            // properties of CoreApplicationViewTitleBar aren't initialized during the first seconds after launch.
            var forceDisplay = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop"
                && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse;

            SetTitleBarVisibility(forceDisplay);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Unregister events
            m_coreTitleBar.LayoutMetricsChanged -= CoreTitleBarLayoutMetricsChanged;
            m_coreTitleBar.IsVisibleChanged -= CoreTitleBarIsVisibleChanged;
            m_uiSettings.ColorValuesChanged -= ColorValuesChanged;
            m_accessibilitySettings.HighContrastChanged -= OnHighContrastChanged;
            Window.Current.Activated -= OnWindowActivated;

            Utils.ThemeHelper.
                UnregisterAppThemeChangedCallback(m_rootFrameRequestedThemeCallbackToken);
        }

        private void RootFrame_RequestedThemeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Frame.RequestedThemeProperty == dp)
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, SetTitleBarControlColors);
            }
        }

        private void CoreTitleBarIsVisibleChanged(CoreApplicationViewTitleBar cTitleBar, object args)
        {
            SetTitleBarVisibility(false);
        }

        private void CoreTitleBarLayoutMetricsChanged(CoreApplicationViewTitleBar cTitleBar, object args)
        {
            SetTitleBarHeightAndPadding();
        }

        private void SetTitleBarVisibility(bool forceDisplay)
        {
            LayoutRoot.Visibility =
                forceDisplay || m_coreTitleBar.IsVisible || IsAlwaysOnTopMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetTitleBarHeightAndPadding()
        {
            if (m_coreTitleBar.Height == 0)
            {
                // The titlebar isn't init
                return;
            }

            double leftAddition;
            double rightAddition;

            if (FlowDirection == FlowDirection.LeftToRight)
            {
                leftAddition = m_coreTitleBar.SystemOverlayLeftInset;
                rightAddition = m_coreTitleBar.SystemOverlayRightInset;
            }
            else
            {
                leftAddition = m_coreTitleBar.SystemOverlayRightInset;
                rightAddition = m_coreTitleBar.SystemOverlayLeftInset;
            }

            LayoutRoot.Padding = new Thickness(leftAddition, 0, rightAddition, 0);
            this.Height = m_coreTitleBar.Height;
        }

        private void ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, SetTitleBarControlColors);
        }

        private void SetTitleBarControlColors()
        {
            var applicationView = ApplicationView.GetForCurrentView();

            var applicationTitleBar = applicationView?.TitleBar;
            if (applicationTitleBar == null)
            {
                return;
            }

            if (m_accessibilitySettings.HighContrast)
            {
                // Reset to use default colors.
                applicationTitleBar.ButtonBackgroundColor = null;
                applicationTitleBar.ButtonForegroundColor = null;
                applicationTitleBar.ButtonInactiveBackgroundColor = null;
                applicationTitleBar.ButtonInactiveForegroundColor = null;
                applicationTitleBar.ButtonHoverBackgroundColor = null;
                applicationTitleBar.ButtonHoverForegroundColor = null;
                applicationTitleBar.ButtonPressedBackgroundColor = null;
                applicationTitleBar.ButtonPressedForegroundColor = null;
            }
            else
            {
                applicationTitleBar.ButtonBackgroundColor = ButtonBackground?.Color;
                applicationTitleBar.ButtonForegroundColor = ButtonForeground?.Color;
                applicationTitleBar.ButtonInactiveBackgroundColor = ButtonInactiveBackground?.Color;
                applicationTitleBar.ButtonInactiveForegroundColor = ButtonInactiveForeground?.Color;
                applicationTitleBar.ButtonHoverBackgroundColor = ButtonHoverBackground?.Color;
                applicationTitleBar.ButtonHoverForegroundColor = ButtonHoverForeground?.Color;
                applicationTitleBar.ButtonPressedBackgroundColor = ButtonPressedBackground?.Color;
                applicationTitleBar.ButtonPressedForegroundColor = ButtonPressedForeground?.Color;
            }
        }

        private void OnHighContrastChanged(Windows.UI.ViewManagement.AccessibilitySettings sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SetTitleBarControlColors();
                SetTitleBarVisibility(false);
            });
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            VisualStateManager.GoToState(
                this, e.WindowActivationState == CoreWindowActivationState.Deactivated ? WindowNotFocused.Name : WindowFocused.Name, false);
        }

        private void OnIsAlwaysOnTopModePropertyChanged(bool oldValue, bool newValue)
        {
            SetTitleBarVisibility(false);
            VisualStateManager.GoToState(this, newValue ? "AOTMiniState" : "AOTNormalState", false);
        }

        private void AlwaysOnTopButton_Click(object sender, RoutedEventArgs e)
        {
            AlwaysOnTopClick?.Invoke(this, e);
        }

        // Dependency properties for the color of the system title bar buttons
        public Windows.UI.Xaml.Media.SolidColorBrush ButtonBackground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonBackgroundProperty);
            set => SetValue(ButtonBackgroundProperty, value);
        }
        public static readonly DependencyProperty ButtonBackgroundProperty =
            DependencyProperty.Register(nameof(ButtonBackground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonForeground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonForegroundProperty);
            set => SetValue(ButtonForegroundProperty, value);
        }
        public static readonly DependencyProperty ButtonForegroundProperty =
            DependencyProperty.Register(nameof(ButtonForeground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonInactiveBackground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonInactiveBackgroundProperty);
            set => SetValue(ButtonInactiveBackgroundProperty, value);
        }
        public static readonly DependencyProperty ButtonInactiveBackgroundProperty =
            DependencyProperty.Register(nameof(ButtonInactiveBackground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonInactiveForeground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonInactiveForegroundProperty);
            set => SetValue(ButtonInactiveForegroundProperty, value);
        }
        public static readonly DependencyProperty ButtonInactiveForegroundProperty =
            DependencyProperty.Register(nameof(ButtonInactiveForeground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonHoverBackground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonHoverBackgroundProperty);
            set => SetValue(ButtonHoverBackgroundProperty, value);
        }
        public static readonly DependencyProperty ButtonHoverBackgroundProperty =
            DependencyProperty.Register(nameof(ButtonHoverBackground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonHoverForeground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonHoverForegroundProperty);
            set => SetValue(ButtonHoverForegroundProperty, value);
        }
        public static readonly DependencyProperty ButtonHoverForegroundProperty =
            DependencyProperty.Register(nameof(ButtonHoverForeground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonPressedBackground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonPressedBackgroundProperty);
            set => SetValue(ButtonPressedBackgroundProperty, value);
        }
        public static readonly DependencyProperty ButtonPressedBackgroundProperty =
            DependencyProperty.Register(nameof(ButtonPressedBackground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public Windows.UI.Xaml.Media.SolidColorBrush ButtonPressedForeground
        {
            get => (Windows.UI.Xaml.Media.SolidColorBrush)GetValue(ButtonPressedForegroundProperty);
            set => SetValue(ButtonPressedForegroundProperty, value);
        }
        public static readonly DependencyProperty ButtonPressedForegroundProperty =
            DependencyProperty.Register(nameof(ButtonPressedForeground), typeof(Windows.UI.Xaml.Media.SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

        public bool BackButtonSpaceReserved
        {
            get => (bool)GetValue(BackButtonSpaceReservedProperty);
            set => SetValue(BackButtonSpaceReservedProperty, value);
        }
        public static readonly DependencyProperty BackButtonSpaceReservedProperty =
            DependencyProperty.Register(
                nameof(BackButtonSpaceReserved), typeof(bool), typeof(TitleBar),
                new PropertyMetadata(false, (sender, args) =>
                {
                    var self = sender as TitleBar;
                    VisualStateManager.GoToState(
                        self, (bool)args.NewValue ? self.BackButtonVisible.Name : self.BackButtonCollapsed.Name, true);
                }));

        private readonly Windows.ApplicationModel.Core.CoreApplicationViewTitleBar m_coreTitleBar;
        private readonly Windows.UI.ViewManagement.UISettings m_uiSettings;
        private readonly Windows.UI.ViewManagement.AccessibilitySettings m_accessibilitySettings;
        private Utils.ThemeHelper.ThemeChangedCallbackToken m_rootFrameRequestedThemeCallbackToken;
    }
}
