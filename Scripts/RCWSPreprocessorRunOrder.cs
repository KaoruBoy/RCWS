#if UNITY_EDITOR
using UnityEditor;

public static class RCWSPreprocessorRunOrder
{
    public const int CallbackOrderVRCFury = -10000;
    public const int CallbackOrderNDMF = CallbackOrderVRCFury - 1000;
    public const int CallbackOrderIEditorOnlyRemoval = -1024;

    const int CallbackOrderMargin = 500;

    const int CallbackOrderAfterVRCFury = CallbackOrderVRCFury + CallbackOrderMargin;
    const int CallbackOrderBeforeVRCFury = CallbackOrderVRCFury - CallbackOrderMargin;
    const int CallbackOrderBeforeNDMF = CallbackOrderNDMF - CallbackOrderMargin;
    const int CallbackOrderAfterNDMF = CallbackOrderAfterVRCFury;

    const int CallbackOrderBeforeIEditorOnlyRemoval = CallbackOrderIEditorOnlyRemoval - 1;
    const int DefaultRCWSCallbackOrder = CallbackOrderBeforeVRCFury;

    const string CallbackOrderPref = "RCWS.CallbackOrder";
    public static int Order
    {
        get => EditorPrefs.GetInt(CallbackOrderPref, DefaultRCWSCallbackOrder);
        set => EditorPrefs.SetInt(CallbackOrderPref, value);
    }
    
    const string OrderMenuLocation = "Tools/RCWS/Preprocessor Order/";
    const string ToolTitleBeforeVRCFury = OrderMenuLocation + "Before VRCFury";
    const string ToolTitleAfterVRCFury = OrderMenuLocation + "After VRCFury";
    const string ToolTitleBeforeNDMF = OrderMenuLocation + "Before NDMF";
    const string ToolTitleBeforeIEditorOnlyRemoval = OrderMenuLocation + "Before IEditorOnly Removal";

    const int ToolPriorityBeforeNDMF = 0;
    const int ToolPriorityBeforeVRCFury = 1;
    const int ToolPriorityAfterVRCFury = 2;
    const int ToolPriorityBeforeIEditorOnlyRemoval = 3;

    [MenuItem(ToolTitleBeforeNDMF, priority = ToolPriorityBeforeNDMF)]
    static void SetOrderBeforeNDMF()
        => Order = CallbackOrderBeforeNDMF;

    [MenuItem(ToolTitleBeforeNDMF, true, priority = ToolPriorityBeforeNDMF)]
    static bool CheckOrderBeforeNDMF()
        => CheckPreprocessorOrder(ToolTitleBeforeNDMF, CallbackOrderBeforeNDMF);

    [MenuItem(ToolTitleBeforeVRCFury, priority = ToolPriorityBeforeVRCFury)]
    static void SetOrderBeforeVRCFury()
        => Order = CallbackOrderBeforeVRCFury;

    [MenuItem(ToolTitleBeforeVRCFury, true, priority = ToolPriorityBeforeVRCFury)]
    static bool CheckOrderBeforeVRCFury()
        => CheckPreprocessorOrder(ToolTitleBeforeVRCFury, CallbackOrderBeforeVRCFury);

    [MenuItem(ToolTitleAfterVRCFury, priority = ToolPriorityAfterVRCFury)]
    static void SetOrderAfterVRCFury()
        => Order = CallbackOrderAfterVRCFury;

    [MenuItem(ToolTitleAfterVRCFury, true, priority = ToolPriorityAfterVRCFury)]
    static bool CheckOrderAfterVRCFury()
        => CheckPreprocessorOrder(ToolTitleAfterVRCFury, CallbackOrderAfterVRCFury);

    [MenuItem(ToolTitleBeforeIEditorOnlyRemoval, priority = ToolPriorityBeforeIEditorOnlyRemoval)]
    static void SetOrderBeforeIEditorOnlyRemoval()
        => Order = CallbackOrderBeforeIEditorOnlyRemoval;

    [MenuItem(ToolTitleBeforeIEditorOnlyRemoval, true, priority = ToolPriorityBeforeIEditorOnlyRemoval)]
    static bool CheckOrderBeforeIEditorOnlyRemoval()
        => CheckPreprocessorOrder(ToolTitleBeforeIEditorOnlyRemoval, CallbackOrderBeforeIEditorOnlyRemoval);

    static bool CheckPreprocessorOrder(string menuPath, int order)
    {
        Menu.SetChecked(menuPath, Order == order);
        return true;
    }
}
#endif