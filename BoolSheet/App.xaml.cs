namespace BoolSheet
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // No UserAppTheme here: the app follows the Windows theme (light or dark).
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell())
            {
                Title = "BoolSheet",
                Width = 1280,
                Height = 800,
                MinimumWidth = 900,
                MinimumHeight = 560,
            };
        }
    }
}
