using Pratyaksh.Core;
using Pratyaksh.UI.UIElements;

namespace Pratyaksh.UI;

public class DemoPanel : UILayoutBase
{
    private int selectedSpace = 15;
    private Raylib_cs.Color currentSelectedColor = Raylib_cs.Color.Black;

    private int scrollViewId;
    private int[] buttonIds;

    private int panelId;
    private int inputFieldId;
    private string fieldText;
    private int nameFieldLabelId;

    private int passwordFieldId;
    private string passwordText;

    private (int id, bool isSelected, Raylib_cs.Color color)[] selectableIds;
    private int scrollView2Id;

    private int dropdownId;
    private int dropdownSelectedIdx;

    private int cycleSelectorId;
    private int cycleSelectedIdx = 0;

    private int customBtnId1;
    private int customBtnId2;
    private int linkBtnId;
    private int statusBadgeId1;
    private int statusBadgeId2;
    private int alertBannerId;
    private int sliderId;
    private float sliderValue = 65f;

    private Selectable? previousSelected = null;

    protected override string PanelName => "DemoPanel";

    public DemoPanel(int posX, int posY, Drawable? parent = null, ParentBasis? parentBasis = null) : base(posX, posY, 410, 480, parent, parentBasis)
    {
        scrollViewId = IdGen.GetNewID();
        buttonIds = [IdGen.GetNewID(), IdGen.GetNewID(), IdGen.GetNewID()];
        
        inputFieldId = IdGen.GetNewID();
        fieldText = "";

        panelId = IdGen.GetNewID();
        nameFieldLabelId = IdGen.GetNewID();

        passwordFieldId = IdGen.GetNewID();
        passwordText = "SecretPass123";
        
        selectableIds = [
                         (IdGen.GetNewID(), false, Raylib_cs.Color.Red),
                         (IdGen.GetNewID(), false, Raylib_cs.Color.SkyBlue),
                         (IdGen.GetNewID(), false, Raylib_cs.Color.Orange),
                         (IdGen.GetNewID(), false, Raylib_cs.Color.Green),
                         (IdGen.GetNewID(), false, Raylib_cs.Color.Magenta),
                         (IdGen.GetNewID(), false, Raylib_cs.Color.Yellow)
                        ];
        
        scrollView2Id = IdGen.GetNewID();
        
        dropdownId = IdGen.GetNewID();
        dropdownSelectedIdx = 0;

        cycleSelectorId = IdGen.GetNewID();
        customBtnId1 = IdGen.GetNewID();
        customBtnId2 = IdGen.GetNewID();
        linkBtnId = IdGen.GetNewID();
        statusBadgeId1 = IdGen.GetNewID();
        statusBadgeId2 = IdGen.GetNewID();
        alertBannerId = IdGen.GetNewID();
        sliderId = IdGen.GetNewID();

        horizontalPadding = 10;
    }

    public override Dictionary<string, object?> GetSaveData()
    {
        return new Dictionary<string, object?>
        {
            ["fieldText"] = fieldText,
            ["passwordText"] = passwordText,
            ["cycleSelectedIdx"] = cycleSelectedIdx,
            ["dropdownSelectedIdx"] = dropdownSelectedIdx,
            ["selectedSpace"] = selectedSpace,
            ["sliderValue"] = sliderValue,
            ["selectableStates"] = selectableIds.Select(s => s.isSelected).ToList()
        };
    }

    public override void RestoreSaveData(System.Text.Json.JsonElement data)
    {
        if (data.ValueKind != System.Text.Json.JsonValueKind.Object) return;

        if (data.TryGetProperty("fieldText", out var ft) && ft.ValueKind == System.Text.Json.JsonValueKind.String)
            fieldText = ft.GetString() ?? "";

        if (data.TryGetProperty("passwordText", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.String)
            passwordText = pt.GetString() ?? "";

        if (data.TryGetProperty("cycleSelectedIdx", out var cs) && cs.ValueKind == System.Text.Json.JsonValueKind.Number)
            cycleSelectedIdx = cs.GetInt32();

        if (data.TryGetProperty("dropdownSelectedIdx", out var dd) && dd.ValueKind == System.Text.Json.JsonValueKind.Number)
            dropdownSelectedIdx = dd.GetInt32();

        if (data.TryGetProperty("selectedSpace", out var ss) && ss.ValueKind == System.Text.Json.JsonValueKind.Number)
            selectedSpace = ss.GetInt32();

        if (data.TryGetProperty("sliderValue", out var sv) && sv.ValueKind == System.Text.Json.JsonValueKind.Number)
            sliderValue = sv.GetSingle();

        if (data.TryGetProperty("selectableStates", out var selArray) && selArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            int idx = 0;
            foreach (var item in selArray.EnumerateArray())
            {
                if (idx < selectableIds.Length && item.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                {
                    bool isSel = item.GetBoolean();
                    selectableIds[idx].isSelected = isSel;
                    if (isSel) currentSelectedColor = selectableIds[idx].color;
                }
                idx++;
            }
        }
    }

    // Callbacks for our interactive elements
    private void OnDemoButtonPressed(Button btn)
    {
        selectedSpace = (int)btn.Payload;
        Console.WriteLine($"[DemoPanel] Button Pressed: {btn.ButtonText} | Payload: {btn.Payload}");
    }

    private void OnDemoSelectablePressed(Selectable sel)
    {
        if (previousSelected != sel && previousSelected != null)
        {
            int prevIdx = (int)previousSelected.Payload;
            
            previousSelected?.Deselect();
            selectableIds[prevIdx].isSelected = false;
        }

        (int id, _, Raylib_cs.Color color) = selectableIds[(int)sel.Payload];

        selectableIds[(int)sel.Payload] = (id, sel.IsSelected, color);
        currentSelectedColor = color;
        
        Console.WriteLine($"[DemoPanel] Selectable Chosen: {sel.SelectableText} | Payload: {sel.Payload}");

        previousSelected = sel;
    }

    public override void OnDrawLayout()
    {
        // 1. Draw the main Section background and header
        (verticalBgOffset, verticalDrawStopOffset) = layout.DrawParentBG(Raylib_cs.Raylib.Fade(Raylib_cs.Color.Gray, 0.65f), 0.08f, "Layout Engine Showcase", Raylib_cs.Raylib.Fade(Raylib_cs.Color.DarkBlue, 0.7f), Raylib_cs.Color.White);

        layout.BeginScrollView(scrollViewId, ContentWidth, RemainingHeight - 10);
        {
            // --- Alert Banner Demonstration ---
            layout.AlertBanner(alertBannerId, "System Status: All services operational", AlertType.Success, ContentWidth - 20, 28);
            
            layout.AddSpace(10);

            // --- Status Badges ---
            layout.BeginHorizontal(35);
            {
                layout.StatusBadge(statusBadgeId1, "Active", StatusType.Active);
                layout.StatusBadge(statusBadgeId2, "Processing", StatusType.Processing);
                layout.LinkButton(linkBtnId, "Open GitHub Repo", "https://github.com/Binary-Tantra/PratyakshLib");
            }
            layout.EndHorizontal(24);

            layout.AddSpace(10);
            
            // --- A simple text element ---
            layout.Text("Welcome to the UI Demo Panel!", 15, Raylib_cs.Color.Gold);
            
            layout.AddSpace(25);

            // Colored Custom Buttons Demonstration
            layout.BeginHorizontal(10);
            {
                layout.Button(customBtnId1, "Primary", 85, 25, OnDemoButtonPressed, 0, fillColor: new Raylib_cs.Color((byte)28, (byte)100, (byte)200, (byte)255), borderColor: Raylib_cs.Color.SkyBlue, textColor: Raylib_cs.Color.White);
                layout.Button(customBtnId2, "Danger", 85, 25, OnDemoButtonPressed, 30, fillColor: new Raylib_cs.Color((byte)180, (byte)40, (byte)40, (byte)255), borderColor: Raylib_cs.Color.Red, textColor: Raylib_cs.Color.White);
                layout.Button(buttonIds[2], "Default", 85, 25, OnDemoButtonPressed, 45);
            }
            layout.EndHorizontal(25);
            
            layout.AddSpace(10);
            
            // --- Cycle Selector ---
            layout.BeginHorizontal(0);
            {
                layout.Text("Theme Mode: ", 15, Raylib_cs.Color.White);

                layout.AddSpace(RemainingWidth - 290);

                layout.CycleSelector(cycleSelectorId, ["Dark Mode", "Light Mode", "High Contrast", "Cyberpunk"], cycleSelectedIdx, 160, 24, (cs) => {
                    cycleSelectedIdx = cs.SelectedIndex;
                });
            }
            layout.EndHorizontal(25);

            layout.AddSpace(10);

            // Form Inputs (Standard text & Masked text)
            layout.BeginPanel(panelId, RemainingWidth, 25, 0, LayoutOpType.Horizontal);
            {
                layout.Text("User Name: ", 15, Raylib_cs.Color.White);

                layout.AddSpace(RemainingWidth - 310);

                string nameField = layout.InputField(inputFieldId, "Type name...", fieldText, 140, 24, (inpf) => fieldText = inpf.InputFieldText).InputFieldText;

                layout.AddSpace(5);

                layout.Label(nameFieldLabelId, nameField, 15, Raylib_cs.Color.White);
            }
            layout.EndPanel();

            layout.AddSpace(5);

            layout.BeginHorizontal(0);
            {
                layout.Text("Password:  ", 15, Raylib_cs.Color.White);

                layout.AddSpace(RemainingWidth - 310);

                layout.InputField(passwordFieldId, "Password...", passwordText, 140, 24, (inpf) => passwordText = inpf.InputFieldText, isMasked: true);
            }
            layout.EndHorizontal(25);

            layout.AddSpace(10);

            // --- Slider Demonstration ---
            layout.BeginHorizontal(60);
            {
                layout.Text("Volume:    ", 15, Raylib_cs.Color.White);
                layout.Slider(sliderId, sliderValue, 0f, 100f, 140, 20, (sl) => sliderValue = sl.Value, format: "{0:0}%", step: 1f);
            }
            layout.EndHorizontal(22);

            layout.AddSpace(10);

            // --- Text Truncation Demonstration ---
            layout.BeginHorizontal(55);
            {
                layout.Text("Long Title: ", 15, Raylib_cs.Color.White);
                layout.TextTruncated("This is a very long text string that will truncate nicely when exceeding width limit", 240, Raylib_cs.Color.LightGray);
            }
            layout.EndHorizontal(20);

            layout.AddSpace(10);

            // --- Nested Selectables & Dropdown ---
            layout.TextPanelPro("Dropdown & Custom Selectables", Width - 30, 25, Raylib_cs.Color.DarkGreen, Raylib_cs.Color.White);

            layout.AddSpace(10);

            int height = 0;
            layout.BeginHorizontal(60);
            {
                layout.Text("Resolution: ", 15, Raylib_cs.Color.White);
                
                height = layout.Dropdown(dropdownId, ["1920x1080", "2560x1440", "3840x2160"], dropdownSelectedIdx, 150, 24, (dd) =>
                {
                    dropdownSelectedIdx = dd.SelectedIndex;
                }, dropdownId).Height;
            }
            layout.EndHorizontal(height);

            layout.AddSpace(15);

            layout.BeginHorizontal(0);
            {
                layout.BeginVertical(0);
                {
                    layout.BeginScrollView(scrollView2Id, 135, 100, 5);
                    {
                        layout.DrawParentBG(Raylib_cs.Color.DarkGray);

                        layout.Selectable(selectableIds[0].id, selectableIds[0].isSelected, "Red", 120, 20, OnDemoSelectablePressed, 0);
                        layout.Selectable(selectableIds[1].id, selectableIds[1].isSelected, "Sky Blue", 120, 20, OnDemoSelectablePressed, 1);
                        layout.Selectable(selectableIds[2].id, selectableIds[2].isSelected, "Orange", 120, 20, OnDemoSelectablePressed, 2);
                        layout.Selectable(selectableIds[3].id, selectableIds[3].isSelected, "Green", 120, 20, OnDemoSelectablePressed, 3);
                        layout.Selectable(selectableIds[4].id, selectableIds[4].isSelected, "Magenta", 120, 20, OnDemoSelectablePressed, 4);
                        layout.Selectable(selectableIds[5].id, selectableIds[5].isSelected, "Yellow", 120, 20, OnDemoSelectablePressed, 5);
                    }
                    layout.EndScrollView();
                }
                layout.EndVertical(135);

                layout.AddSpace(20);

                // Right Column: Panel displaying selected color
                layout.Panel(RemainingWidth - 20, 100, currentSelectedColor);
            }
            layout.EndHorizontal(100);
        }
        layout.EndScrollView();
    }
}