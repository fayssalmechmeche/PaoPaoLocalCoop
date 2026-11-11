using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Screen choice: split screen (both windows side by side on the main screen) or one screen per player,
// and which player goes on which screen. Shown by the launcher when 2 screens or more are connected.
internal static class ScreenSetup
{
    public sealed class Choice
    {
        public bool Dual, DontAsk;
        public string Screen1 = "", Screen2 = "";
    }

    private static bool _ui;

    // Must run before any window is created. Per-monitor DPI awareness gives screen sizes in physical pixels,
    // which is what the game windows use.
    public static void InitUi()
    {
        if (_ui) return;
        _ui = true;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }

    // Screens from left to right (then top to bottom): "Screen 1" is the leftmost one.
    public static Screen[] Ordered() => Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();

    // Id stored in options.ini, e.g. DISPLAY2.
    public static string Id(Screen s) => s.DeviceName.Replace(@"\\.\", "");

    public static Screen Find(Screen[] screens, string id) =>
        string.IsNullOrWhiteSpace(id) ? null : screens.FirstOrDefault(s => string.Equals(Id(s), id.Trim(), StringComparison.OrdinalIgnoreCase));

    // "x,y,width,height" in physical pixels, passed to the game with --coop-rect.
    public static string Rect(Screen s, bool workArea)
    {
        var r = workArea ? s.WorkingArea : s.Bounds;
        return $"{r.X},{r.Y},{r.Width},{r.Height}";
    }

    // Returns null if the window is closed or cancelled.
    public static Choice Ask(Screen[] screens, Choice current, Func<string, string, string> T, string title)
    {
        int n = screens.Length;
        string Label(int i) => T("Ecran ", "Screen ") + (i + 1) + (screens[i].Primary ? T(" (principal)", " (main)") : "") +
                               "  -  " + screens[i].Bounds.Width + " x " + screens[i].Bounds.Height;

        using var form = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScaleMode = AutoScaleMode.Dpi, Font = SystemFonts.MessageBoxFont, TopMost = true, Padding = new Padding(12),
        };
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        void Row(Control c, int span = 2)
        {
            c.Margin = new Padding(3, 4, 3, 4);
            grid.Controls.Add(c);
            grid.SetColumnSpan(c, span);
        }
        Label Text(string s) => new Label { Text = s, AutoSize = true, MaximumSize = new Size(560, 0) };

        Row(Text(n >= 2
            ? T(n + " ecrans detectes. Comment voulez-vous jouer ?", n + " screens detected. How do you want to play?")
            : T("Un seul ecran detecte : seul l'ecran partage est possible.", "Only one screen detected: only split screen is possible.")));
        var split = new RadioButton { Text = T("Ecran partage : les deux fenetres cote a cote sur l'ecran principal",
                                               "Split screen: both windows side by side on the main screen"), AutoSize = true };
        var dual = new RadioButton { Text = T("Un ecran par joueur", "One screen per player"), AutoSize = true, Enabled = n >= 2 };
        Row(split);
        Row(dual);

        var cb1 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        var cb2 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        for (int i = 0; i < n; i++) { cb1.Items.Add(Label(i)); cb2.Items.Add(Label(i)); }
        int primary = Math.Max(0, Array.FindIndex(screens, s => s.Primary));
        int i1 = Array.FindIndex(screens, s => Id(s).Equals(current.Screen1, StringComparison.OrdinalIgnoreCase));
        int i2 = Array.FindIndex(screens, s => Id(s).Equals(current.Screen2, StringComparison.OrdinalIgnoreCase));
        if (i1 < 0) i1 = primary;
        if (i2 < 0 || i2 == i1) i2 = n >= 2 ? Enumerable.Range(0, n).First(i => i != i1) : i1;
        cb1.SelectedIndex = i1;
        cb2.SelectedIndex = i2;

        var l1 = Text(T("Joueur 1 :", "Player 1:"));
        var l2 = Text(T("Joueur 2 :", "Player 2:"));
        l1.Anchor = l2.Anchor = AnchorStyles.Left;
        Row(l1, 1); Row(cb1, 1);
        Row(l2, 1); Row(cb2, 1);
        Row(Text(T("Avec une seule manette, elle commande le joueur 2 ; le joueur 1 joue au clavier/souris.",
                   "With a single controller, it controls player 2; player 1 uses keyboard/mouse.")));

        var identify = new Button { Text = T("Identifier les ecrans", "Identify screens"), AutoSize = true };
        identify.Click += (_, _) => Identify(screens);
        Row(identify);

        var dontAsk = new CheckBox
        {
            Text = T("Ne plus demander (pour changer plus tard : PaoPaoLocalCoop.exe --screens)",
                     "Don't ask again (to change it later: PaoPaoLocalCoop.exe --screens)"),
            AutoSize = true, Checked = current.DontAsk,
        };
        Row(dontAsk);

        var ok = new Button { Text = T("Jouer", "Play"), AutoSize = true };
        var cancel = new Button { Text = T("Annuler", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        Row(buttons);

        void Sync() => cb1.Enabled = cb2.Enabled = dual.Checked;
        dual.CheckedChanged += (_, _) => Sync();
        (current.Dual && n >= 2 ? dual : split).Checked = true;
        Sync();

        ok.Click += (_, _) =>
        {
            if (dual.Checked && cb1.SelectedIndex == cb2.SelectedIndex)
            {
                MessageBox.Show(form, T("Choisis deux ecrans differents.", "Choose two different screens."), title,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            form.DialogResult = DialogResult.OK;
        };
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Controls.Add(grid);

        if (form.ShowDialog() != DialogResult.OK) return null;
        return new Choice
        {
            Dual = dual.Checked,
            DontAsk = dontAsk.Checked,
            Screen1 = Id(screens[cb1.SelectedIndex]),
            Screen2 = Id(screens[cb2.SelectedIndex]),
        };
    }

    // Shows the screen number in big on each screen for a few seconds.
    private static void Identify(Screen[] screens)
    {
        var shown = new Form[screens.Length];
        for (int i = 0; i < screens.Length; i++)
        {
            var b = screens[i].Bounds;
            int size = Math.Min(b.Width, b.Height) / 3;
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false,
                TopMost = true, BackColor = Color.Black, AutoScaleMode = AutoScaleMode.None,
                Bounds = new Rectangle(b.X + (b.Width - size) / 2, b.Y + (b.Height - size) / 2, size, size),
            };
            f.Controls.Add(new Label
            {
                Text = (i + 1).ToString(), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(FontFamily.GenericSansSerif, size / 2f, FontStyle.Bold, GraphicsUnit.Pixel),
            });
            f.Show();
            f.Bounds = new Rectangle(b.X + (b.Width - size) / 2, b.Y + (b.Height - size) / 2, size, size);
            shown[i] = f;
        }
        var timer = new Timer { Interval = 2500 };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            foreach (var f in shown) f.Dispose();
        };
        timer.Start();
    }
}
