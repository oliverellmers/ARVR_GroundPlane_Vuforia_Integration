/*============================================================================== 
Copyright (c) 2017-2018 PTC Inc. All Rights Reserved.

Vuforia is a trademark of PTC Inc., registered in the United States and other 
countries.   
==============================================================================*/

using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class PlaneManager : MonoBehaviour
{
    public enum PlaneMode
    {
        NONE,
        //GROUND,
        //MIDAIR,
        PLACEMENT
    }

    #region PUBLIC_MEMBERS
    public PlaneFinderBehaviour m_PlaneFinder;

    [Header("Placement Augmentations")]
    public GameObject m_PlacementAugmentation;

    public static bool GroundPlaneHitReceived;
    public static PlaneMode planeMode = PlaneMode.PLACEMENT;

    public static bool AnchorExists
    {
        get { return anchorExists; }
        private set { anchorExists = value; }
    }

    #endregion // PUBLIC_MEMBERS


    #region PRIVATE_MEMBERS
    const string unsupportedDeviceTitle = "Unsupported Device";
    const string unsupportedDeviceBody =
        "This device has failed to start the Positional Device Tracker. " +
        "Please check the list of supported Ground Plane devices on our site: " +
        "\n\nhttps://library.vuforia.com/articles/Solution/ground-plane-supported-devices.html";

    StateManager m_StateManager;
    SmartTerrain m_SmartTerrain;
    PositionalDeviceTracker m_PositionalDeviceTracker;
    ContentPositioningBehaviour m_ContentPositioningBehaviour;

    TouchHandler m_TouchHandler;
    ProductPlacement m_ProductPlacement;
    AnchorBehaviour /*m_PlaneAnchor, m_MidAirAnchor,*/ m_PlacementAnchor;

    GroundPlaneUI m_GroundPlaneUI; 
    TrackableBehaviour.StatusInfo m_StatusInfo;
    int AutomaticHitTestFrameCount;
    int m_AnchorCounter;
    bool uiHasBeenInitialized;
    static bool anchorExists; // backs public AnchorExists property

    private ARRenderedCanvasManager arRCM;

    #endregion // PRIVATE_MEMBERS


    #region MONOBEHAVIOUR_METHODS

    void Start()
    {
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
        VuforiaARController.Instance.RegisterOnPauseCallback(OnVuforiaPaused);
        DeviceTrackerARController.Instance.RegisterTrackerStartedCallback(OnTrackerStarted);
        DeviceTrackerARController.Instance.RegisterDevicePoseStatusChangedCallback(OnDevicePoseStatusChanged);

        m_PlaneFinder.HitTestMode = HitTestMode.AUTOMATIC;

        m_ProductPlacement = m_PlacementAugmentation.GetComponentInChildren<ProductPlacement>();
        m_TouchHandler = m_PlacementAugmentation.GetComponentInChildren<TouchHandler>();
        m_PlacementAnchor = m_PlacementAugmentation.GetComponentInParent<AnchorBehaviour>();

        m_GroundPlaneUI = FindObjectOfType<GroundPlaneUI>();

        arRCM = GameObject.FindGameObjectWithTag("FXCanvas").GetComponent<ARRenderedCanvasManager>();

        UtilityHelper.EnableRendererColliderCanvas(m_PlacementAugmentation, false);

    }

    void Update()
    {

        if (!VuforiaRuntimeUtilities.IsPlayMode() && !AnchorExists)
        {
            AnchorExists = DoAnchorsExist();
        }

        GroundPlaneHitReceived = (AutomaticHitTestFrameCount == Time.frameCount);

        SetSurfaceIndicatorVisible(
            GroundPlaneHitReceived &&
            (/*planeMode == PlaneMode.GROUND || */(planeMode == PlaneMode.PLACEMENT && Input.touchCount == 0)));
    }

    void OnDestroy()
    {
        Debug.Log("OnDestroy() called.");

        VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
        VuforiaARController.Instance.UnregisterOnPauseCallback(OnVuforiaPaused);
        DeviceTrackerARController.Instance.UnregisterTrackerStartedCallback(OnTrackerStarted);
        DeviceTrackerARController.Instance.UnregisterDevicePoseStatusChangedCallback(OnDevicePoseStatusChanged);
    }

    #endregion // MONOBEHAVIOUR_METHODS


    #region GROUNDPLANE_CALLBACKS

    public void HandleAutomaticHitTest(HitTestResult result)
    {
        AutomaticHitTestFrameCount = Time.frameCount;

        if (!uiHasBeenInitialized)
        {
            uiHasBeenInitialized = m_GroundPlaneUI.InitializeUI();
        }

        if (planeMode == PlaneMode.PLACEMENT && !m_ProductPlacement.IsPlaced)
        {
            SetSurfaceIndicatorVisible(false);
            m_ProductPlacement.SetProductAnchor(null);
            m_PlacementAugmentation.PositionAt(result.Position);
        }
    }

    public void HandleInteractiveHitTest(HitTestResult result)
    {
        if (result == null)
        {
            Debug.LogError("Invalid hit test result!");
            return;
        }

        if (m_StatusInfo == TrackableBehaviour.StatusInfo.NORMAL ||
            (m_StatusInfo == TrackableBehaviour.StatusInfo.UNKNOWN && !VuforiaRuntimeUtilities.IsPlayMode()))
        {
            if (!m_GroundPlaneUI.IsCanvasButtonPressed())
            {
                Debug.Log("HandleInteractiveHitTest() called.");

                // If the PlaneFinderBehaviour's Mode is Automatic, then the Interactive HitTestResult will be centered.

                // PlaneMode.Ground and PlaneMode.Placement both use PlaneFinder's ContentPositioningBehaviour
                m_ContentPositioningBehaviour = m_PlaneFinder.GetComponent<ContentPositioningBehaviour>();
                m_ContentPositioningBehaviour.DuplicateStage = false;

                // Place object based on Ground Plane mode
                switch (planeMode)
                {
                    case PlaneMode.NONE:
                        /*
                        m_ContentPositioningBehaviour.AnchorStage = m_PlaneAnchor;
                        m_ContentPositioningBehaviour.PositionContentAtPlaneAnchor(result);
                        UtilityHelper.EnableRendererColliderCanvas(m_PlaneAugmentation, true);

                        // Astronaut should rotate toward camera with each placement
                        m_PlaneAugmentation.transform.localPosition = Vector3.zero;
                        UtilityHelper.RotateTowardCamera(m_PlaneAugmentation);
                        */
                        UtilityHelper.EnableRendererColliderCanvas(m_PlacementAugmentation, false);
                        ResetScene();
                        ResetTrackers();

                        arRCM.FadeCanvas(false);

                        break;
                    /*

                case PlaneMode.GROUND:

                    m_ContentPositioningBehaviour.AnchorStage = m_PlaneAnchor;
                    m_ContentPositioningBehaviour.PositionContentAtPlaneAnchor(result);
                    UtilityHelper.EnableRendererColliderCanvas(m_PlaneAugmentation, true);

                    // Astronaut should rotate toward camera with each placement
                    m_PlaneAugmentation.transform.localPosition = Vector3.zero;
                    UtilityHelper.RotateTowardCamera(m_PlaneAugmentation);

                    break;
                    */


                    case PlaneMode.PLACEMENT:

                        if (!m_ProductPlacement.IsPlaced || TouchHandler.DoubleTap)
                        {
                            m_ContentPositioningBehaviour.AnchorStage = m_PlacementAnchor;
                            m_ContentPositioningBehaviour.PositionContentAtPlaneAnchor(result);
                            UtilityHelper.EnableRendererColliderCanvas(m_PlacementAugmentation, true);

                            arRCM.FadeCanvas(true);

                        }

                        if (!m_ProductPlacement.IsPlaced)
                        {
                            m_ProductPlacement.SetProductAnchor(m_PlacementAnchor.transform);
                            m_TouchHandler.enableRotation = true;
                        }

                        break;
                }
            }
        }

    }

    #endregion // GROUNDPLANE_CALLBACKS


    #region PUBLIC_BUTTON_METHODS


    public void SetPlacementMode(bool active)
    {
        if (active)
        {
            planeMode = PlaneMode.PLACEMENT;
            //m_GroundPlaneUI.UpdateTitle();
            m_PlaneFinder.enabled = true;
            //m_MidAirPositioner.enabled = false;
            m_TouchHandler.enableRotation = m_PlacementAugmentation.activeInHierarchy;
        }

        //Here we are setting it off when off!
        else {
            planeMode = PlaneMode.NONE;
            m_PlaneFinder.enabled = false;
            //m_TouchHandler.enableRotation = false

            ResetScene();
            ResetTrackers();
        }
    }

    public void ResetScene()
    {
        Debug.Log("ResetScene() called.");

        m_ProductPlacement.Reset();
        UtilityHelper.EnableRendererColliderCanvas(m_PlacementAugmentation, false);

        DeleteAnchors();
        m_ProductPlacement.SetProductAnchor(null);
        m_GroundPlaneUI.Reset();
        m_TouchHandler.enableRotation = false;
    }

    public void ResetTrackers()
    {
        Debug.Log("ResetTrackers() called.");

        m_SmartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();
        m_PositionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();

        // Stop and restart trackers
        m_SmartTerrain.Stop(); // stop SmartTerrain tracker before PositionalDeviceTracker
        m_PositionalDeviceTracker.Stop();
        m_PositionalDeviceTracker.Start();
        m_SmartTerrain.Start(); // start SmartTerrain tracker after PositionalDeviceTracker
    }

    #endregion // PUBLIC_BUTTON_METHODS


    #region PRIVATE_METHODS

    void DeleteAnchors()
    {
        m_PlacementAnchor.UnConfigureAnchor();
        AnchorExists = DoAnchorsExist();
    }

    void SetSurfaceIndicatorVisible(bool isVisible)
    {
        Renderer[] renderers = m_PlaneFinder.PlaneIndicator.GetComponentsInChildren<Renderer>(true);
        Canvas[] canvas = m_PlaneFinder.PlaneIndicator.GetComponentsInChildren<Canvas>(true);

        foreach (Canvas c in canvas)
            c.enabled = isVisible;

        foreach (Renderer r in renderers)
            r.enabled = isVisible;
    }

    bool DoAnchorsExist()
    {
        if (m_StateManager != null)
        {
            IEnumerable<TrackableBehaviour> trackableBehaviours = m_StateManager.GetActiveTrackableBehaviours();

            foreach (TrackableBehaviour behaviour in trackableBehaviours)
            {
                if (behaviour is AnchorBehaviour)
                {
                    return true;
                }
            }
        }
        return false;
    }

    #endregion // PRIVATE_METHODS


    #region VUFORIA_CALLBACKS

    void OnVuforiaStarted()
    {
        Debug.Log("OnVuforiaStarted() called.");

        m_StateManager = TrackerManager.Instance.GetStateManager();

        // Check trackers to see if started and start if necessary
        m_PositionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();
        m_SmartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();

        if (m_PositionalDeviceTracker != null && m_SmartTerrain != null)
        {
            if (!m_PositionalDeviceTracker.IsActive)
                m_PositionalDeviceTracker.Start();
            if (m_PositionalDeviceTracker.IsActive && !m_SmartTerrain.IsActive)
                m_SmartTerrain.Start();
        }
        else
        {
            if (m_PositionalDeviceTracker == null)
                Debug.Log("PositionalDeviceTracker returned null. GroundPlane not supported on this device.");
            if (m_SmartTerrain == null)
                Debug.Log("SmartTerrain returned null. GroundPlane not supported on this device.");

            MessageBox.DisplayMessageBox(unsupportedDeviceTitle, unsupportedDeviceBody, false, null);
        }
    }

    void OnVuforiaPaused(bool paused)
    {
        Debug.Log("OnVuforiaPaused(" + paused.ToString() + ") called.");

        if (paused)
            ResetScene();
    }

    #endregion // VUFORIA_CALLBACKS


    #region DEVICE_TRACKER_CALLBACKS

    void OnTrackerStarted()
    {
        Debug.Log("OnTrackerStarted() called.");

        m_PositionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();
        m_SmartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();

        if (m_PositionalDeviceTracker != null)
        {
            if (!m_PositionalDeviceTracker.IsActive)
                m_PositionalDeviceTracker.Start();

            Debug.Log("PositionalDeviceTracker is Active?: " + m_PositionalDeviceTracker.IsActive +
                      "\nSmartTerrain Tracker is Active?: " + m_SmartTerrain.IsActive);
        }
    }

    void OnDevicePoseStatusChanged(TrackableBehaviour.Status status, TrackableBehaviour.StatusInfo statusInfo)
    {
        Debug.Log("OnDevicePoseStatusChanged(" + status + ", " + statusInfo + ")");

        m_StatusInfo = statusInfo;
    }

    #endregion // DEVICE_TRACKER_CALLBACK_METHODS
}
