namespace System.Windows.Automation
{
    // Accessibility names have no effect headless; kept so the desktop sources compile unchanged.
    public static class AutomationProperties
    {
        public static void SetName(DependencyObject element, string value)
        {
        }

        public static string GetName(DependencyObject element) => string.Empty;
    }
}
