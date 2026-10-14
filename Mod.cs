using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Tools;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(WorkersPlaces.Mod), "WorkersPlaces", "0.1.0", "AndreValvassori")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace WorkersPlaces;

public sealed class Mod : MelonMod
{
    internal static Mod? Instance { get; private set; }
    private readonly Dictionary<Guid, IdleOverride> _overrides = new();
    private string? _savePath;
    private bool _hasSavedPositions;
    private LoadManager? _loadManager;
    private Employee? _selectedWorker;
    private Employee? _placingWorker;
    private ManagementClipboard? _clipboard;
    private ManagementClipboard_Equippable? _equippable;
    private Il2CppSystem.Collections.Generic.List<IConfigurable>? _selection;
    private bool _placing;
    private ClipboardIdleControls? _clipboardControls;
    private PlacementPrompt? _placementPrompt;
    private string _placementMessage = string.Empty;
    private float _nextLoadCheck;
    private float _nextControlsCheck;
    private GUIStyle? _labelStyle;
    private GUIStyle? _titleStyle;

    public override void OnInitializeMelon()
    {
        Instance = this;
        HarmonyInstance.PatchAll(typeof(Mod).Assembly);
        LoggerInstance.Msg("WorkersPlaces initialized. Open a worker's management clipboard to set an idle position.");
    }

    public override void OnUpdate()
    {
        if (_savePath == null && Time.unscaledTime >= _nextLoadCheck)
        {
            _nextLoadCheck = Time.unscaledTime + 1f;
            TryLoadSave();
        }

        if (!_placing)
        {
            if (_clipboard != null && _clipboard.IsOpen && _selectedWorker != null)
            {
                if (_clipboardControls == null && Time.unscaledTime >= _nextControlsCheck)
                {
                    _nextControlsCheck = Time.unscaledTime + 0.5f;
                    try
                    {
                        _clipboardControls = ClipboardIdleControls.TryCreate(_selectedWorker.Type, StartSelectedPlacement, ClearSelectedPosition);
                    }
                    catch (Exception exception)
                    {
                        LoggerInstance.Warning($"Could not attach clipboard controls: {exception.Message}");
                        _nextControlsCheck = float.PositiveInfinity;
                    }
                }

                RefreshClipboardControls();
                _clipboardControls?.HandlePointerClick(_clipboard.OverlayCamera);
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            FinishPlacement();
        }
        else if (Input.GetKeyDown(KeyCode.Space) && _placingWorker != null)
        {
            try
            {
                Player? player = Player.Local;
                if (player == null)
                {
                    SetPlacementMessage("Player is not ready. Try again in a moment.");
                    return;
                }

                Vector3 position = player.PlayerBasePosition;
                float yaw = player.CameraRotation.eulerAngles.y;
                Guid workerId = GetWorkerId(_placingWorker);
                if (WorkerPositions.Set(workerId, new WorkerPosition(position.x, position.y, position.z, yaw)))
                {
                    _hasSavedPositions = true;
                    ApplyOverride(_placingWorker);
                    LoggerInstance.Msg($"Idle position set for {_placingWorker.FullName}.");
                    FinishPlacement();
                }
                else
                {
                    SetPlacementMessage("Could not save position. Check the MelonLoader log.");
                }
            }
            catch (Exception exception)
            {
                LoggerInstance.Error($"Could not set idle position: {exception}");
                SetPlacementMessage("Could not save position. Check the MelonLoader log.");
            }
        }
    }

    public override void OnGUI()
    {
        if (_placing)
        {
            if (_placementPrompt == null)
            {
                DrawPlacement();
            }

            return;
        }

        if (_savePath == null || _clipboard == null || !_clipboard.IsOpen || _selectedWorker == null)
        {
            return;
        }

        if (_clipboardControls != null)
        {
            return;
        }

        Employee worker = _selectedWorker;

        EnsureStyles();
        float width = Math.Min(360f, Screen.width * 0.34f);
        Rect panel = new(Screen.width - width - 24f, Screen.height * 0.57f, width, 114f);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, 28f), "Idle position", _titleStyle);

        if (!InstanceFinder.IsServer)
        {
            GUI.Label(new Rect(panel.x + 12f, panel.y + 45f, width - 24f, 55f), "Only the host can change worker positions.", _labelStyle);
            return;
        }

        Guid workerId;
        try
        {
            workerId = GetWorkerId(worker);
        }
        catch
        {
            return;
        }

        bool hasPosition = WorkerPositions.TryGet(workerId, out _);
        string status = hasPosition ? "Custom" : "Default";
        GUI.Label(new Rect(panel.x + 12f, panel.y + 37f, width - 24f, 20f), status, _labelStyle);
        if (GUI.Button(new Rect(panel.x + 12f, panel.y + 70f, 130f, 30f), hasPosition ? "Change" : "Set"))
        {
            BeginPlacement(worker);
        }

        if (hasPosition && GUI.Button(new Rect(panel.x + 150f, panel.y + 70f, 100f, 30f), "Clear"))
        {
            ClearSelectedPosition();
        }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        ClearSession();
        _loadManager = null;
        _clipboard = null;
        _selectedWorker = null;
        DestroyClipboardControls();
        _nextLoadCheck = 0f;
    }

    private void TryLoadSave()
    {
        _loadManager ??= UnityEngine.Object.FindObjectOfType<LoadManager>();
        SaveInfo? saveInfo = _loadManager != null && _loadManager.IsGameLoaded ? _loadManager.ActiveSaveInfo : null;
        string? activePath = saveInfo?.SavePath;

        if (string.IsNullOrWhiteSpace(activePath))
        {
            return;
        }

        try
        {
            WorkerPositions.Initialize(activePath, warning => LoggerInstance.Warning(warning));
            _savePath = activePath;
            _hasSavedPositions = WorkerPositions.HasAny;
            LoggerInstance.Msg($"Loaded worker positions for save '{saveInfo!.SaveSlotNumber}'.");
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"Could not load worker positions: {exception}");
            _nextLoadCheck = Time.unscaledTime + 10f;
            return;
        }

        if (!InstanceFinder.IsServer || !_hasSavedPositions)
        {
            return;
        }

        foreach (Employee employee in UnityEngine.Object.FindObjectsOfType<Employee>())
        {
            try
            {
                Guid workerId = GetWorkerId(employee);
                if (!employee.Fired && WorkerPositions.TryGet(workerId, out _))
                {
                    ApplyOverride(employee);
                }
            }
            catch (Exception exception)
            {
                LoggerInstance.Warning($"Could not initialize an employee's idle position: {exception.Message}");
            }
        }
    }

    private void ClearSession()
    {
        if (_placing)
        {
            FinishPlacement(reopenClipboard: false);
        }

        foreach (Guid workerId in _overrides.Keys.ToArray())
        {
            RestoreOverride(workerId);
        }

        WorkerPositions.Reset();
        _hasSavedPositions = false;
        _savePath = null;
    }

    private static Guid GetWorkerId(Employee employee)
    {
        if (!Guid.TryParse(employee.GUID.ToString(), out Guid workerId) || workerId == Guid.Empty)
        {
            throw new InvalidOperationException("Employee has no persistent GUID yet.");
        }

        return workerId;
    }

    internal void ApplyOverride(Employee employee)
    {
        if (!_hasSavedPositions || _savePath == null || !InstanceFinder.IsServer || employee.WaitOutside == null)
        {
            return;
        }

        Guid workerId = GetWorkerId(employee);
        if (!WorkerPositions.TryGet(workerId, out WorkerPosition storedPosition))
        {
            RestoreOverride(workerId);
            return;
        }

        Vector3 target = new(storedPosition.X, storedPosition.Y, storedPosition.Z);
        if (_overrides.TryGetValue(workerId, out IdleOverride? existing) && existing.Worker == employee && existing.Target != null)
        {
            if (employee.WaitOutside.IdlePoint != existing.Target)
            {
                existing.Original = employee.WaitOutside.IdlePoint;
                employee.WaitOutside.IdlePoint = existing.Target;
            }

            if (Vector3.Distance(existing.Target.position, target) > 0.01f)
            {
                existing.Target.position = target;
            }

            if (storedPosition.Yaw.HasValue)
            {
                existing.Target.rotation = Quaternion.Euler(0f, storedPosition.Yaw.Value, 0f);
            }

            return;
        }

        if (_overrides.ContainsKey(workerId))
        {
            RestoreOverride(workerId);
        }

        GameObject marker = new($"WorkersPlaces Idle Target {workerId:N}");
        marker.hideFlags = HideFlags.HideAndDontSave;
        marker.transform.position = target;
        Transform? original = employee.WaitOutside.IdlePoint;
        marker.transform.rotation = storedPosition.Yaw.HasValue
            ? Quaternion.Euler(0f, storedPosition.Yaw.Value, 0f)
            : original != null ? original.rotation : Quaternion.identity;
        _overrides[workerId] = new IdleOverride(employee, original, marker.transform);
        employee.WaitOutside.IdlePoint = marker.transform;
    }

    internal void ApplyOverrideSafely(Employee employee)
    {
        try
        {
            ApplyOverride(employee);
        }
        catch (Exception exception)
        {
            LoggerInstance.Warning($"Could not apply idle position: {exception.Message}");
        }
    }

    internal void RemoveWorker(Employee employee)
    {
        if (!InstanceFinder.IsServer || _savePath == null)
        {
            return;
        }

        try
        {
            Guid workerId = GetWorkerId(employee);
            WorkerPositions.Clear(workerId);
            _hasSavedPositions = WorkerPositions.HasAny;
            RestoreOverride(workerId);
        }
        catch (Exception exception)
        {
            LoggerInstance.Warning($"Could not remove fired worker's idle position: {exception.Message}");
        }
    }

    private void RestoreOverride(Guid workerId)
    {
        if (!_overrides.Remove(workerId, out IdleOverride? entry))
        {
            return;
        }

        if (entry.Worker != null && entry.Worker.WaitOutside != null && entry.Worker.WaitOutside.IdlePoint == entry.Target)
        {
            entry.Worker.WaitOutside.IdlePoint = entry.Original;
        }

        if (entry.Target != null)
        {
            UnityEngine.Object.Destroy(entry.Target.gameObject);
        }
    }

    internal void OnClipboardOpened(ManagementClipboard clipboard)
    {
        DestroyClipboardControls();
        _clipboard = clipboard;
        _selectedWorker = null;
        if (clipboard.CurrentConfigurables == null || clipboard.CurrentConfigurables.Count != 1)
        {
            return;
        }

        _selectedWorker = clipboard.CurrentConfigurables[0]?.TryCast<Employee>();
        _nextControlsCheck = 0f;
    }

    internal void OnClipboardClosed(ManagementClipboard clipboard)
    {
        if (_clipboard != clipboard)
        {
            return;
        }

        _selectedWorker = null;
        DestroyClipboardControls();
        if (!_placing)
        {
            _clipboard = null;
        }
    }

    private void BeginPlacement(Employee worker)
    {
        if (_clipboard == null)
        {
            return;
        }

        _selection = new Il2CppSystem.Collections.Generic.List<IConfigurable>();
        _selection.Add(_clipboard.CurrentConfigurables[0]);
        _equippable = UnityEngine.Object.FindObjectOfType<ManagementClipboard_Equippable>();
        if (_equippable == null)
        {
            _selection = null;
            LoggerInstance.Warning("Clipboard equip component was not found; placement did not start.");
            return;
        }
        _placingWorker = worker;
        _placing = true;
        SetPlacementMessage("Stand at idle spot, face desired direction, press Space");
        try
        {
            _placementPrompt = PlacementPrompt.TryCreate(_equippable.OverrideText, _placementMessage);
        }
        catch (Exception exception)
        {
            LoggerInstance.Warning($"Could not show placement prompt: {exception.Message}");
        }

        _clipboard.Close();
    }

    private void SetPlacementMessage(string message)
    {
        _placementMessage = message;
        _placementPrompt?.SetText(message);
    }

    private void StartSelectedPlacement()
    {
        if (InstanceFinder.IsServer && _selectedWorker != null)
        {
            BeginPlacement(_selectedWorker);
        }
    }

    private void ClearSelectedPosition()
    {
        if (!InstanceFinder.IsServer || _selectedWorker == null)
        {
            return;
        }

        try
        {
            Guid workerId = GetWorkerId(_selectedWorker);
            if (WorkerPositions.Clear(workerId))
            {
                _hasSavedPositions = WorkerPositions.HasAny;
                RestoreOverride(workerId);
                RefreshClipboardControls();
            }
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"Could not clear idle position: {exception}");
        }
    }

    private void RefreshClipboardControls()
    {
        if (_clipboardControls == null || _selectedWorker == null)
        {
            return;
        }

        bool hasPosition = Guid.TryParse(_selectedWorker.GUID.ToString(), out Guid workerId) && WorkerPositions.TryGet(workerId, out _);
        _clipboardControls.Refresh(hasPosition, InstanceFinder.IsServer);
    }

    private void DestroyClipboardControls()
    {
        _clipboardControls?.Destroy();
        _clipboardControls = null;
    }

    private void FinishPlacement(bool reopenClipboard = true)
    {
        _placing = false;
        _placingWorker = null;
        _placementPrompt?.Destroy();
        _placementPrompt = null;
        if (reopenClipboard && _clipboard != null && _selection != null && _equippable != null)
        {
            try
            {
                _clipboard.Open(_selection, _equippable);
            }
            catch (Exception exception)
            {
                LoggerInstance.Warning($"Could not reopen clipboard automatically: {exception.Message}");
            }
        }

        _selection = null;
    }

    private void DrawPlacement()
    {
        EnsureStyles();
        GUI.Box(new Rect(16f, 16f, 470f, 70f), GUIContent.none);
        GUI.Label(new Rect(28f, 27f, 440f, 50f), _placementMessage, _labelStyle);
    }

    private void EnsureStyles()
    {
        _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        _titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
    }

    private sealed class IdleOverride
    {
        public IdleOverride(Employee worker, Transform? original, Transform target)
        {
            Worker = worker;
            Original = original;
            Target = target;
        }

        public Employee Worker { get; }
        public Transform? Original { get; set; }
        public Transform Target { get; }
    }
}

[HarmonyPatch(typeof(Employee), nameof(Employee.SetIdle), typeof(bool))]
internal static class EmployeeIdlePatch
{
    private static void Prefix(Employee __instance, bool idle)
    {
        if (idle)
        {
            Mod.Instance?.ApplyOverrideSafely(__instance);
        }
    }
}

[HarmonyPatch(typeof(Employee), nameof(Employee.ReceiveFire))]
internal static class EmployeeFiredPatch
{
    private static void Prefix(Employee __instance)
    {
        Mod.Instance?.RemoveWorker(__instance);
    }
}

[HarmonyPatch(typeof(ManagementClipboard), nameof(ManagementClipboard.Open))]
internal static class ClipboardOpenedPatch
{
    private static void Postfix(ManagementClipboard __instance)
    {
        Mod.Instance?.OnClipboardOpened(__instance);
    }
}

[HarmonyPatch(typeof(ManagementClipboard), nameof(ManagementClipboard.Close))]
internal static class ClipboardClosedPatch
{
    private static void Postfix(ManagementClipboard __instance)
    {
        Mod.Instance?.OnClipboardClosed(__instance);
    }
}

[HarmonyPatch(typeof(ManagementClipboard), nameof(ManagementClipboard.CloseAndPreserveState))]
internal static class ClipboardPreservedPatch
{
    private static void Postfix(ManagementClipboard __instance)
    {
        Mod.Instance?.OnClipboardClosed(__instance);
    }
}
