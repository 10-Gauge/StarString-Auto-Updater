using System.Drawing;
using System.Windows.Forms;

namespace StarStringsAutoUpdater.UI;

/// <summary>Small dialog for entering a custom "check every H hours, M minutes" interval.</summary>
public sealed class CustomIntervalForm : Form
{
    private const int MinTotalMinutes = 5;
    private const int MaxHours = 168; // 1 week

    private readonly NumericUpDown _hoursInput;
    private readonly NumericUpDown _minutesInput;

    private int TotalMinutes => (int)_hoursInput.Value * 60 + (int)_minutesInput.Value;

    public CustomIntervalForm(int initialTotalMinutes)
    {
        Text = "Custom Check Interval";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(300, 130);

        var label = new Label
        {
            Text = "Check for StarStrings updates every:",
            AutoSize = true,
            Location = new Point(16, 16),
        };

        var hoursLabel = new Label { Text = "Hours:", AutoSize = true, Location = new Point(16, 52) };
        _hoursInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = MaxHours,
            Value = Math.Clamp(initialTotalMinutes / 60, 0, MaxHours),
            Location = new Point(70, 48),
            Width = 60,
        };

        var minutesLabel = new Label { Text = "Minutes:", AutoSize = true, Location = new Point(150, 52) };
        _minutesInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 59,
            Value = Math.Clamp(initialTotalMinutes % 60, 0, 59),
            Location = new Point(214, 48),
            Width = 60,
        };

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(114, 88),
            Size = new Size(80, 28),
        };
        okButton.Click += OnOkClick;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(200, 88),
            Size = new Size(80, 28),
        };

        Controls.AddRange([label, hoursLabel, _hoursInput, minutesLabel, _minutesInput, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        if (TotalMinutes < MinTotalMinutes)
        {
            MessageBox.Show(
                $"Please choose an interval of at least {MinTotalMinutes} minutes.",
                "Invalid interval", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
    }

    /// <summary>Shows the dialog and returns true (with the chosen total minutes) if confirmed.</summary>
    public static bool TryAskForInterval(int currentTotalMinutes, out int totalMinutes)
    {
        using var form = new CustomIntervalForm(currentTotalMinutes);
        var accepted = form.ShowDialog() == DialogResult.OK;
        totalMinutes = form.TotalMinutes;
        return accepted;
    }
}
