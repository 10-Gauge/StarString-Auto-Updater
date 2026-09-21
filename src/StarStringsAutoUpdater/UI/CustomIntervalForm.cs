using System.Drawing;
using System.Windows.Forms;

namespace StarStringsAutoUpdater.UI;

/// <summary>Small dialog for entering a custom "check every H hours, M minutes" interval.</summary>
public sealed class CustomIntervalForm : Form
{
    private const int MinTotalMinutes = 5;
    private const int MaxHours = 168; // 1 week

    private const int Margin = 16;
    private const int LabelInputGap = 10;
    private const int InputWidth = 70;
    private const int RowSpacing = 14;
    private const int ButtonWidth = 80;
    private const int ButtonHeight = 28;

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

        const string introText = "Check for StarStrings updates every:";
        const string hoursText = "Hours:";
        const string minutesText = "Minutes:";

        var introLabel = new Label
        {
            Text = introText,
            AutoSize = true,
            Location = new Point(Margin, Margin),
        };

        // Measure both row labels so the input fields start at a shared column wide
        // enough for either one, regardless of font/DPI - this is what actually
        // prevents the labels overlapping the fields (fixed pixel offsets don't).
        var hoursLabelSize = TextRenderer.MeasureText(hoursText, Font);
        var minutesLabelSize = TextRenderer.MeasureText(minutesText, Font);
        var labelColumnWidth = Math.Max(hoursLabelSize.Width, minutesLabelSize.Width);
        var inputX = Margin + labelColumnWidth + LabelInputGap;

        var hoursRowTop = introLabel.Bottom + 16;
        var hoursLabel = new Label
        {
            Text = hoursText,
            AutoSize = true,
            Location = new Point(Margin, hoursRowTop + 3),
        };
        _hoursInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = MaxHours,
            Value = Math.Clamp(initialTotalMinutes / 60, 0, MaxHours),
            Location = new Point(inputX, hoursRowTop),
            Width = InputWidth,
        };

        var minutesRowTop = hoursRowTop + _hoursInput.Height + RowSpacing;
        var minutesLabel = new Label
        {
            Text = minutesText,
            AutoSize = true,
            Location = new Point(Margin, minutesRowTop + 3),
        };
        _minutesInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 59,
            Value = Math.Clamp(initialTotalMinutes % 60, 0, 59),
            Location = new Point(inputX, minutesRowTop),
            Width = InputWidth,
        };

        var contentWidth = Math.Max(introLabel.Width, labelColumnWidth + LabelInputGap + InputWidth);
        var dialogWidth = Margin * 2 + contentWidth;
        var buttonsTop = minutesRowTop + _minutesInput.Height + 20;

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(dialogWidth - Margin - ButtonWidth * 2 - 8, buttonsTop),
            Size = new Size(ButtonWidth, ButtonHeight),
        };
        okButton.Click += OnOkClick;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(okButton.Right + 8, buttonsTop),
            Size = new Size(ButtonWidth, ButtonHeight),
        };

        ClientSize = new Size(dialogWidth, buttonsTop + ButtonHeight + Margin);

        Controls.AddRange([introLabel, hoursLabel, _hoursInput, minutesLabel, _minutesInput, okButton, cancelButton]);
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
