using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

public class DragonExample : MonoBehaviour
{
    private enum AirAnimationMode
    {
        HoverIdle,
        FlyingForward
    }

    private enum DragonRideState
    {
        UnmountedIdleRestless,
        MountedGroundIdle,
        MountedGroundWalk,
        MountedTakeOff,
        MountedAirHoverMove,
        MountedLanding
    }

    [Header("Mount")]
    [SerializeField] private float mountDistance = 6f;
    [SerializeField] private Vector3 mountOffset = new Vector3(0f, 2.5f, -1.5f);
    [SerializeField] private Vector3 dismountOffset = new Vector3(2f, 0f, 0f);

    [Header("Movement")]
    [SerializeField] private float groundMoveSpeed = 4f;
    [SerializeField] private float airMoveSpeed = 8f;
    [SerializeField] private float yawSpeed = 120f;

    [Header("Flight")]
    [SerializeField] private float takeoffRiseHeight = 8f;
    [SerializeField] private float takeoffDuration = 1.2f;
    [SerializeField] private float landingDuration = 1.2f;
    [SerializeField] private float takeoffToHoverNormalizedTime = 0.95f;
    [SerializeField] private float landingGroundEpsilon = 0.05f;
    [SerializeField] private float airForwardLiftY = 18f;

    [Header("Rider")]
    [SerializeField] private Transform riderRootOverride;
    [SerializeField] private string riderObjectName = "FPSController";

    [Header("Third Person Camera")]
    [SerializeField] private Camera mainRideCameraOverride;
    [SerializeField] private Camera firstPersonCameraOverride;
    [SerializeField] private string mainRideCameraName = "long";
    [SerializeField] private Vector3 followCameraOffset = new Vector3(0f, 3.2f, -8f);
    [SerializeField] private Vector3 followLookAtOffset = new Vector3(0f, 1.8f, 2.5f);
    [SerializeField] private float followSmooth = 8f;

    private const string IdleSimpleParam = "IdleSimple";
    private const string IdleAgressiveParam = "IdleAgressive";
    private const string IdleRestlessParam = "IdleRestless";
    private const string WalkParam = "Walk";
    private const string BattleStanceParam = "BattleStance";
    private const string BiteParam = "Bite";
    private const string DrakarisParam = "Drakaris";
    private const string FlyingFwdParam = "FlyingFWD";
    private const string FlyingAttackParam = "FlyingAttack";
    private const string HoverParam = "Hover";
    private const string LandsParam = "Lands";
    private const string TakeOffParam = "TakeOff";
    private const string DieParam = "Die";
    private const string PreferredRideCameraName = "long";

    private Animator anim;

    private int idleSimpleHash;
    private int idleAgressiveHash;
    private int idleRestlessHash;
    private int walkHash;
    private int battleStanceHash;
    private int biteHash;
    private int drakarisHash;
    private int flyingFwdHash;
    private int flyingAttackHash;
    private int hoverHash;
    private int landsHash;
    private int takeOffHash;
    private int dieHash;
    private int[] allAnimationParams;

    private DragonRideState state;
    private bool stateInitialized;
    private AirAnimationMode airAnimationMode;
    private bool airAnimationModeInitialized;

    private Transform riderRoot;
    private Transform riderOriginalParent;
    private FirstPersonController firstPersonController;
    private CharacterController characterController;
    private Rigidbody riderRigidbody;
    private bool riderOriginalUseGravity;
    private bool riderOriginalIsKinematic;
    private bool riderMissingLogged;

    private Camera mainRideCamera;
    private Camera firstPersonCamera;
    private AudioListener mainRideAudioListener;
    private AudioListener firstPersonAudioListener;
    private bool mountedCameraMode;
    private bool cameraSnapped;
    private bool rideCameraPoseCaptured;
    private Vector3 rideCameraLocalOffset;
    private Quaternion rideCameraLocalRotation;
    private Transform mainRideCameraOriginalParent;
    private bool mainRideCameraOriginalParentCaptured;

    private float groundY;
    private Vector3 takeoffStartPosition;
    private float takeoffElapsed;
    private float landingStartY;
    private float landingElapsed;

    private void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("DragonExample requires an Animator on the same GameObject.", this);
            enabled = false;
            return;
        }

        CacheAnimatorHashes();
        ResolveRiderReferences();
        ResolveCameraReferences();
        CaptureRideCameraPoseFromScene();

        groundY = transform.position.y;

        EnterState(DragonRideState.UnmountedIdleRestless);
        SetMountedCameraMode(false, true);
    }

    private void Update()
    {
        HandleMountInput();

        if (IsMounted())
        {
            HandleMountedYaw();
        }

        switch (state)
        {
            case DragonRideState.UnmountedIdleRestless:
                UpdateUnmountedIdleRestless();
                break;
            case DragonRideState.MountedGroundIdle:
                UpdateMountedGroundIdle();
                break;
            case DragonRideState.MountedGroundWalk:
                UpdateMountedGroundWalk();
                break;
            case DragonRideState.MountedTakeOff:
                UpdateMountedTakeOff();
                break;
            case DragonRideState.MountedAirHoverMove:
                UpdateMountedAirHoverMove();
                break;
            case DragonRideState.MountedLanding:
                UpdateMountedLanding();
                break;
        }
    }

    private void LateUpdate()
    {
        UpdateRideCameraFollow();
    }

    private void CacheAnimatorHashes()
    {
        idleSimpleHash = Animator.StringToHash(IdleSimpleParam);
        idleAgressiveHash = Animator.StringToHash(IdleAgressiveParam);
        idleRestlessHash = Animator.StringToHash(IdleRestlessParam);
        walkHash = Animator.StringToHash(WalkParam);
        battleStanceHash = Animator.StringToHash(BattleStanceParam);
        biteHash = Animator.StringToHash(BiteParam);
        drakarisHash = Animator.StringToHash(DrakarisParam);
        flyingFwdHash = Animator.StringToHash(FlyingFwdParam);
        flyingAttackHash = Animator.StringToHash(FlyingAttackParam);
        hoverHash = Animator.StringToHash(HoverParam);
        landsHash = Animator.StringToHash(LandsParam);
        takeOffHash = Animator.StringToHash(TakeOffParam);
        dieHash = Animator.StringToHash(DieParam);

        allAnimationParams = new[]
        {
            idleSimpleHash,
            idleAgressiveHash,
            idleRestlessHash,
            walkHash,
            battleStanceHash,
            biteHash,
            drakarisHash,
            flyingFwdHash,
            flyingAttackHash,
            hoverHash,
            landsHash,
            takeOffHash,
            dieHash
        };
    }

    private void HandleMountInput()
    {
        if (state == DragonRideState.UnmountedIdleRestless && Input.GetKeyDown(KeyCode.F))
        {
            TryMount();
        }
    }

    private void ResolveRiderReferences()
    {
        if (riderRootOverride != null)
        {
            riderRoot = riderRootOverride;
        }
        else if (riderRoot == null && !string.IsNullOrEmpty(riderObjectName))
        {
            GameObject riderObject = GameObject.Find(riderObjectName);
            if (riderObject != null)
            {
                riderRoot = riderObject.transform;
            }
        }

        if (riderRoot == null)
        {
            return;
        }

        if (firstPersonController == null)
        {
            firstPersonController = riderRoot.GetComponent<FirstPersonController>();
        }

        if (characterController == null)
        {
            characterController = riderRoot.GetComponent<CharacterController>();
        }

        if (riderRigidbody == null)
        {
            riderRigidbody = riderRoot.GetComponent<Rigidbody>();
        }

        if (firstPersonCamera == null)
        {
            if (firstPersonCameraOverride != null)
            {
                firstPersonCamera = firstPersonCameraOverride;
            }
            else
            {
                firstPersonCamera = riderRoot.GetComponentInChildren<Camera>(true);
            }
        }

        if (firstPersonAudioListener == null && firstPersonCamera != null)
        {
            firstPersonAudioListener = firstPersonCamera.GetComponent<AudioListener>();
        }
    }

    private void ResolveCameraReferences()
    {
        if (mainRideCamera == null)
        {
            if (mainRideCameraOverride != null)
            {
                mainRideCamera = mainRideCameraOverride;
            }
            else
            {
                GameObject preferredCameraObject = GameObject.Find(PreferredRideCameraName);
                if (preferredCameraObject != null)
                {
                    mainRideCamera = preferredCameraObject.GetComponent<Camera>();
                }

                if (mainRideCamera == null && !string.IsNullOrEmpty(mainRideCameraName))
                {
                    GameObject mainCameraObject = GameObject.Find(mainRideCameraName);
                    if (mainCameraObject != null)
                    {
                        mainRideCamera = mainCameraObject.GetComponent<Camera>();
                    }
                }
            }
        }

        if (mainRideAudioListener == null && mainRideCamera != null)
        {
            mainRideAudioListener = mainRideCamera.GetComponent<AudioListener>();
        }

        if (firstPersonCamera == null && firstPersonCameraOverride != null)
        {
            firstPersonCamera = firstPersonCameraOverride;
        }

        if (firstPersonAudioListener == null && firstPersonCamera != null)
        {
            firstPersonAudioListener = firstPersonCamera.GetComponent<AudioListener>();
        }
    }

    private void CaptureRideCameraPoseFromScene()
    {
        if (mainRideCamera == null)
        {
            return;
        }

        rideCameraLocalOffset = transform.InverseTransformPoint(mainRideCamera.transform.position);
        rideCameraLocalRotation = Quaternion.Inverse(transform.rotation) * mainRideCamera.transform.rotation;
        rideCameraPoseCaptured = true;
    }

    private void AttachRideCameraToDragon()
    {
        if (mainRideCamera == null)
        {
            return;
        }

        if (!mainRideCameraOriginalParentCaptured)
        {
            mainRideCameraOriginalParent = mainRideCamera.transform.parent;
            mainRideCameraOriginalParentCaptured = true;
        }

        mainRideCamera.transform.SetParent(transform, true);

        if (rideCameraPoseCaptured)
        {
            mainRideCamera.transform.localPosition = rideCameraLocalOffset;
            mainRideCamera.transform.localRotation = rideCameraLocalRotation;
        }
    }

    private void DetachRideCameraFromDragon()
    {
        if (mainRideCamera == null || !mainRideCameraOriginalParentCaptured)
        {
            return;
        }

        mainRideCamera.transform.SetParent(mainRideCameraOriginalParent, true);
    }

    private bool IsMounted()
    {
        return state != DragonRideState.UnmountedIdleRestless;
    }

    private void TryMount()
    {
        ResolveRiderReferences();
        ResolveCameraReferences();
        CaptureRideCameraPoseFromScene();

        if (riderRoot == null)
        {
            if (!riderMissingLogged)
            {
                Debug.LogWarning(
                    "DragonExample could not find rider object. Assign riderRootOverride or keep riderObjectName as FPSController.",
                    this);
                riderMissingLogged = true;
            }

            return;
        }

        float distanceToRider = Vector3.Distance(riderRoot.position, transform.position);
        if (distanceToRider > mountDistance)
        {
            return;
        }

        riderOriginalParent = riderRoot.parent;

        if (riderRigidbody != null)
        {
            riderOriginalUseGravity = riderRigidbody.useGravity;
            riderOriginalIsKinematic = riderRigidbody.isKinematic;
            riderRigidbody.velocity = Vector3.zero;
            riderRigidbody.angularVelocity = Vector3.zero;
            riderRigidbody.useGravity = false;
            riderRigidbody.isKinematic = true;
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = false;
        }

        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
        }

        riderRoot.SetParent(transform, false);
        riderRoot.localPosition = mountOffset;
        riderRoot.localRotation = Quaternion.identity;

        groundY = transform.position.y;
        SetMountedCameraMode(true, true);
        EnterState(DragonRideState.MountedGroundIdle);
    }

    private void Dismount()
    {
        if (state != DragonRideState.MountedGroundIdle && state != DragonRideState.MountedGroundWalk)
        {
            return;
        }

        if (riderRoot != null)
        {
            Vector3 dismountWorldPosition = transform.TransformPoint(dismountOffset);

            if (characterController != null && characterController.enabled)
            {
                characterController.enabled = false;
            }

            riderRoot.SetParent(riderOriginalParent, true);
            riderRoot.position = dismountWorldPosition;
            riderRoot.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        if (riderRigidbody != null)
        {
            riderRigidbody.isKinematic = riderOriginalIsKinematic;
            riderRigidbody.useGravity = riderOriginalUseGravity;
            riderRigidbody.velocity = Vector3.zero;
            riderRigidbody.angularVelocity = Vector3.zero;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = true;
        }

        SetMountedCameraMode(false, true);
        EnterState(DragonRideState.UnmountedIdleRestless);
    }

    private void HandleMountedYaw()
    {
        float yawInput = Input.GetAxis("Mouse X");
        if (Mathf.Abs(yawInput) < 0.0001f)
        {
            return;
        }

        float yaw = yawInput * yawSpeed * Time.deltaTime;
        transform.Rotate(0f, yaw, 0f, Space.World);
    }

    private void UpdateUnmountedIdleRestless()
    {
        // Intentionally empty: this state only keeps the dragon in IdleRestless.
    }

    private void UpdateMountedGroundIdle()
    {
        KeepDragonAtGroundY();

        if (Input.GetKeyDown(KeyCode.G))
        {
            Dismount();
            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            BeginTakeOff();
            return;
        }

        if (Input.GetKey(KeyCode.W))
        {
            EnterState(DragonRideState.MountedGroundWalk);
        }
    }

    private void UpdateMountedGroundWalk()
    {
        KeepDragonAtGroundY();

        if (Input.GetKeyDown(KeyCode.G))
        {
            Dismount();
            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            BeginTakeOff();
            return;
        }

        if (!Input.GetKey(KeyCode.W))
        {
            EnterState(DragonRideState.MountedGroundIdle);
            return;
        }

        MoveForward(groundMoveSpeed, true);
    }

    private void BeginTakeOff()
    {
        groundY = transform.position.y;
        EnterState(DragonRideState.MountedTakeOff);
    }

    private void UpdateMountedTakeOff()
    {
        takeoffElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, takeoffDuration);
        float t = Mathf.Clamp01(takeoffElapsed / duration);
        float smoothT = SmoothStep01(t);

        Vector3 nextPosition = takeoffStartPosition;
        nextPosition.y = Mathf.Lerp(takeoffStartPosition.y, groundY + takeoffRiseHeight, smoothT);
        transform.position = nextPosition;

        bool takeOffAnimDone = false;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(TakeOffParam))
        {
            takeOffAnimDone = stateInfo.normalizedTime >= takeoffToHoverNormalizedTime;
        }

        if (takeOffAnimDone || t >= 1f)
        {
            EnterState(DragonRideState.MountedAirHoverMove);
        }
    }

    private void UpdateMountedAirHoverMove()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            BeginLanding();
            return;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            ApplyAirForwardLift();
        }

        if (Input.GetKey(KeyCode.W))
        {
            SetAirAnimationMode(AirAnimationMode.FlyingForward);
            MoveForward(airMoveSpeed, false);
            return;
        }

        SetAirAnimationMode(AirAnimationMode.HoverIdle);
    }

    private void ApplyAirForwardLift()
    {
        Vector3 liftedPosition = transform.position;
        liftedPosition.y += airForwardLiftY;
        transform.position = liftedPosition;
    }

    private void BeginLanding()
    {
        EnterState(DragonRideState.MountedLanding);
    }

    private void UpdateMountedLanding()
    {
        landingElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, landingDuration);
        float t = Mathf.Clamp01(landingElapsed / duration);
        float smoothT = SmoothStep01(t);

        Vector3 nextPosition = transform.position;
        nextPosition.y = Mathf.Lerp(landingStartY, groundY, smoothT);
        transform.position = nextPosition;

        if (t >= 1f || Mathf.Abs(transform.position.y - groundY) <= landingGroundEpsilon)
        {
            KeepDragonAtGroundY();
            EnterState(DragonRideState.MountedGroundIdle);
        }
    }

    private void MoveForward(float speed, bool lockToGround)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();
        Vector3 nextPosition = transform.position + forward * speed * Time.deltaTime;

        if (lockToGround)
        {
            nextPosition.y = groundY;
        }

        transform.position = nextPosition;
    }

    private void KeepDragonAtGroundY()
    {
        Vector3 clampedPosition = transform.position;
        clampedPosition.y = groundY;
        transform.position = clampedPosition;
    }

    private void EnterState(DragonRideState newState)
    {
        if (stateInitialized && state == newState)
        {
            return;
        }

        state = newState;
        stateInitialized = true;

        switch (state)
        {
            case DragonRideState.UnmountedIdleRestless:
                SetExclusiveAnimation(idleRestlessHash);
                ForcePlayState(IdleRestlessParam);
                break;
            case DragonRideState.MountedGroundIdle:
                SetExclusiveAnimation(idleSimpleHash);
                ForcePlayState(IdleSimpleParam);
                KeepDragonAtGroundY();
                break;
            case DragonRideState.MountedGroundWalk:
                SetExclusiveAnimation(walkHash);
                ForcePlayState(WalkParam);
                KeepDragonAtGroundY();
                break;
            case DragonRideState.MountedTakeOff:
                takeoffElapsed = 0f;
                takeoffStartPosition = transform.position;
                SetExclusiveAnimation(takeOffHash);
                ForcePlayState(TakeOffParam);
                break;
            case DragonRideState.MountedAirHoverMove:
                SetAirAnimationMode(AirAnimationMode.HoverIdle);
                break;
            case DragonRideState.MountedLanding:
                landingElapsed = 0f;
                landingStartY = transform.position.y;
                SetExclusiveAnimation(landsHash);
                ForcePlayState(LandsParam);
                break;
        }
    }

    private void SetExclusiveAnimation(int activeHash)
    {
        if (anim == null || allAnimationParams == null)
        {
            return;
        }

        for (int i = 0; i < allAnimationParams.Length; i++)
        {
            anim.SetBool(allAnimationParams[i], false);
        }

        anim.SetBool(activeHash, true);
    }

    private void ForcePlayState(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!anim.HasState(0, stateHash))
        {
            return;
        }

        anim.CrossFadeInFixedTime(stateName, 0.1f, 0, 0f);
    }

    private void SetAirAnimationMode(AirAnimationMode newMode)
    {
        if (airAnimationModeInitialized && airAnimationMode == newMode)
        {
            return;
        }

        airAnimationMode = newMode;
        airAnimationModeInitialized = true;

        switch (newMode)
        {
            case AirAnimationMode.HoverIdle:
                SetExclusiveAnimation(hoverHash);
                ForcePlayState(HoverParam);
                break;
            case AirAnimationMode.FlyingForward:
                SetExclusiveAnimation(flyingFwdHash);
                ForcePlayState(FlyingFwdParam);
                break;
        }
    }

    private void SetMountedCameraMode(bool mounted, bool snapImmediately)
    {
        ResolveCameraReferences();

        mountedCameraMode = mounted;
        cameraSnapped = false;

        bool hasFirstPersonCamera = firstPersonCamera != null;
        bool hasMainRideCamera = mainRideCamera != null;

        if (hasMainRideCamera)
        {
            bool enableMainRide = mounted || !hasFirstPersonCamera;
            mainRideCamera.enabled = enableMainRide;
            if (mainRideAudioListener != null)
            {
                mainRideAudioListener.enabled = enableMainRide;
            }
        }

        if (hasFirstPersonCamera)
        {
            bool enableFirstPerson = !mounted;
            firstPersonCamera.enabled = enableFirstPerson;
            if (firstPersonAudioListener != null)
            {
                firstPersonAudioListener.enabled = enableFirstPerson;
            }
        }

        if (mounted)
        {
            if (!rideCameraPoseCaptured)
            {
                CaptureRideCameraPoseFromScene();
            }

            AttachRideCameraToDragon();
        }
        else
        {
            DetachRideCameraFromDragon();
        }

        if (mounted && snapImmediately)
        {
            SnapRideCamera();
        }
    }

    private void UpdateRideCameraFollow()
    {
        if (!mountedCameraMode || mainRideCamera == null)
        {
            return;
        }

        if (!cameraSnapped)
        {
            SnapRideCamera();
        }

        if (mainRideCamera.transform.parent == transform && rideCameraPoseCaptured)
        {
            mainRideCamera.transform.localPosition = rideCameraLocalOffset;
            mainRideCamera.transform.localRotation = rideCameraLocalRotation;
            return;
        }

        if (rideCameraPoseCaptured)
        {
            mainRideCamera.transform.position = transform.TransformPoint(rideCameraLocalOffset);
            mainRideCamera.transform.rotation = transform.rotation * rideCameraLocalRotation;
            return;
        }

        Vector3 desiredPosition = transform.TransformPoint(followCameraOffset);
        float smoothFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSmooth) * Time.deltaTime);
        mainRideCamera.transform.position = Vector3.Lerp(mainRideCamera.transform.position, desiredPosition, smoothFactor);

        Vector3 lookTarget = transform.TransformPoint(followLookAtOffset);
        Vector3 lookDirection = lookTarget - mainRideCamera.transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            mainRideCamera.transform.rotation = Quaternion.Slerp(mainRideCamera.transform.rotation, desiredRotation, smoothFactor);
        }
    }

    private void SnapRideCamera()
    {
        if (mainRideCamera == null)
        {
            return;
        }

        if (mainRideCamera.transform.parent == transform && rideCameraPoseCaptured)
        {
            mainRideCamera.transform.localPosition = rideCameraLocalOffset;
            mainRideCamera.transform.localRotation = rideCameraLocalRotation;
            cameraSnapped = true;
            return;
        }

        if (rideCameraPoseCaptured)
        {
            mainRideCamera.transform.position = transform.TransformPoint(rideCameraLocalOffset);
            mainRideCamera.transform.rotation = transform.rotation * rideCameraLocalRotation;
            cameraSnapped = true;
            return;
        }

        Vector3 desiredPosition = transform.TransformPoint(followCameraOffset);
        mainRideCamera.transform.position = desiredPosition;

        Vector3 lookTarget = transform.TransformPoint(followLookAtOffset);
        Vector3 lookDirection = lookTarget - desiredPosition;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            mainRideCamera.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        cameraSnapped = true;
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
