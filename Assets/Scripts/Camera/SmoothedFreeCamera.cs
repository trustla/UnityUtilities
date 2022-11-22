using UnityEngine;
using UnityEngine.InputSystem;

public class SmoothedFreeCamera : MonoBehaviour
{
    private Camera _camera;
    // Camera motion params
    // ------------------------------------------------------------------------------------------------------------\
    private Vector3 _pivot;
    private Vector3 _pivotTarget;

    // NOTE: the current value is got from '_camera.transform.position'
    private Vector3 _translationTarget;

    private InternalMotion<float> _horizontalRotation;
    private InternalMotion<float> _verticalRotation;

    private InternalMotion<Quaternion> _freeOrbitRotation;

    // NOTE: the current value is got from '_camera.orthographicSize'
    private float _zoomTarget;

    private bool _onRotation;
    private bool _onMotion;

    private Vector2 _screenCenter;

    private InputAction _mouseAction;
    private InputAction _dragAction;

    private string _lastMouseButton;

    private Vector2 _lastMousePosition;

    private float _dragDistance;

    private float _dragThreshold = 1;

    bool _zoomComplete = true;

    #region Enums

    private enum MotionType
    {
        Translation,
        Rotation,
        Zoom,
        Fit
    }

    #endregion

    private sealed class InternalMotion<T>
    {
        #region Constructors

        public InternalMotion(T current)
        {
            this.Current = current;
            this.Target = current;
        }

        public InternalMotion(T current, T target)
        {
            this.Current = current;
            this.Target = target;
        }

        #endregion

        #region Properties

        public T Current { get; set; }

        public T Target { get; set; }

        #endregion

        public void ResetTarget()
        {
            this.Target = this.Current;
        }
    }

    #region Editor Properties

    public float SmoothingSpeed = 10.0f;

    public float ZoomSpeed = 1.0f;

    public float ZoomClampingMinValue = 0.0f;

    public float ZoomClampingMaxValue = 1000.0f;

    public float RotateHorizontalSpeed = 1.0f;

    public float RotateVerticalSpeed = 1.0f;

    #endregion

    void Awake()
    {
        _camera = Camera.main;
        _camera.orthographic = false;

        _camera.transform.position = Vector3.zero;
        _camera.transform.rotation = Quaternion.identity;

        _pivot = Vector3.zero;
        _pivotTarget = _pivot;

        _translationTarget = _camera.transform.position;

        _onRotation = false;
        _onMotion = false;

        _horizontalRotation = new InternalMotion<float>(_camera.transform.eulerAngles.y);
        _verticalRotation = new InternalMotion<float>(_camera.transform.eulerAngles.x);

        _freeOrbitRotation = new InternalMotion<Quaternion>(_camera.transform.rotation);

        _screenCenter = new Vector2( Screen.width * 0.5f, Screen.height * 0.5f);


        _mouseAction = new InputAction();

        _mouseAction.AddBinding(Mouse.current.leftButton);
        _mouseAction.AddBinding(Mouse.current.rightButton);
        _mouseAction.AddBinding(Mouse.current.middleButton);
        _mouseAction.AddBinding(Mouse.current.scroll);

        _mouseAction.started += this.OnMouseEvent;
        _mouseAction.performed += this.OnMouseEvent;
        _mouseAction.canceled += this.OnMouseEvent;

        _mouseAction.Enable();

        // - Handling mouse drag -----------------------------------------------------------------------------------
        _dragAction = new InputAction();

        // Warning: Mouse.current.delta seems to be dependent on mouse speed (into UnityEditor)
        _dragAction.AddBinding(Mouse.current.position);
        _dragAction.performed += this.OnMouseEvent;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_onMotion)
        {
            return;
        }

        var smoothing = Time.deltaTime * SmoothingSpeed;

        if (_onRotation)
        {
            // - Calculates rotation -------------------------------------------------------------------------------
            var oldRotation = _camera.transform.rotation;

            _freeOrbitRotation.Current = Quaternion.Slerp(_freeOrbitRotation.Current, _freeOrbitRotation.Target, smoothing);
            _camera.transform.rotation = _freeOrbitRotation.Current;

            var cameraPosition = _camera.transform.position;
            var inversedOldRotation = Quaternion.Inverse(oldRotation);
            _translationTarget = ((_camera.transform.rotation * inversedOldRotation) * (cameraPosition - _pivot)) + _pivot;
            _camera.transform.position = _translationTarget;
        }
        else
        {
            // - Calculates translation ----------------------------------------------------------------------------
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, _translationTarget, smoothing);
            _pivot = Vector3.Lerp(_pivot, _pivotTarget, smoothing);
        }

        if (_camera.orthographic)
        {
            // - Calculates zoom ---------------------------------------------------------------------------------------
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _zoomTarget, smoothing);
            _zoomComplete = Mathf.Approximately(_camera.orthographicSize, _zoomTarget);
        }

        // - Checks if the camera is still in motion ---------------------------------------------------------------
        if (_onRotation)
        {
            bool rotationCompleted;

                // '==' compares the closeness of dot product of two quaternions to 1
            rotationCompleted = _freeOrbitRotation.Current == _freeOrbitRotation.Target;


            if (rotationCompleted && _zoomComplete)
            {
                this.StopMotion();
            }
        }
        else
        {
            if (MathHelper.Approximately(_camera.transform.position, _translationTarget) &&
                MathHelper.Approximately(_pivot, _pivotTarget) &&
                _zoomComplete)
            {
                this.StopMotion();
            }
        }
    }

    private void StartMotion(MotionType motionType)
    {
        if (motionType == MotionType.Translation ||
            motionType == MotionType.Fit)
        {
            _horizontalRotation.ResetTarget();
            _verticalRotation.ResetTarget();

            _freeOrbitRotation.ResetTarget();

            _onRotation = false;
        }
        else if (motionType == MotionType.Rotation)
        {
            _onRotation = true;
        }

        _onMotion = true;
    }

    private void StopMotion()
    {
        // To avoid a 'remote' float overflow; instead vertical rotation is already clamped
        _horizontalRotation.Current = _horizontalRotation.Current % 360;
        _horizontalRotation.ResetTarget();

        _onRotation = false;
        _onMotion = false;
    }

    public void Pan(Vector2 drag)
    {
        // Inversion of the current drag vector
        drag = drag * -1.0f;

        var normalizedDragX = ((_camera.orthographicSize * _camera.aspect) / _screenCenter.x) * drag.x;
        var normalizedDragY = (_camera.orthographicSize / _screenCenter.y) * drag.y;

        var xTranslation = _camera.transform.right * normalizedDragX;
        var yTranslation = _camera.transform.up * normalizedDragY;

        _pivotTarget += xTranslation + yTranslation;

        _translationTarget += xTranslation + yTranslation;

        this.StartMotion(MotionType.Translation);
    }

    public void Rotate(Vector2 drag)
    {
        var horizontalSpeed = RotateHorizontalSpeed;
        var verticalSpeed = RotateVerticalSpeed;

        var angleY = horizontalSpeed * drag.x;
        var angleX = verticalSpeed * (drag.y * -1.0f);

        if (!_onRotation)
        {
            _freeOrbitRotation.Current = _camera.transform.rotation;
        }

        _freeOrbitRotation.Target = _freeOrbitRotation.Target *
                                    Quaternion.AngleAxis(angleY, Vector3.up) * Quaternion.AngleAxis(angleX, Vector3.right);

       

        // Realigns pivots
        _pivotTarget = _pivot;

        this.StartMotion(MotionType.Rotation);
    }

    public void Zoom(float scrollValue)
    {
        if (_camera.orthographic)
        {
            _zoomComplete = false;
            var normalizedValue = (scrollValue * ZoomSpeed) * _camera.orthographicSize;

            _zoomTarget = _camera.orthographicSize + normalizedValue;
            _zoomTarget = Mathf.Clamp(_zoomTarget, ZoomClampingMinValue, ZoomClampingMaxValue);
        }
        else
        {
            _zoomComplete = true;
            _translationTarget += (scrollValue * ZoomSpeed) * _camera.transform.forward;
        }

        this.StartMotion(MotionType.Zoom);
    }

    private void OnMouseEvent(InputAction.CallbackContext context)
    {
        // - InputActionPhase.Started ------------------------------------------------------------------------------
        if (context.started)
        {
            var activeControl = context.action.activeControl;
            if (activeControl != Mouse.current.scroll)
            {
                _lastMouseButton = activeControl.name;
                _lastMousePosition = Mouse.current.position.ReadValue();

                _dragDistance = 0.0f;
                _dragAction.Enable();
            }
            else if (activeControl == Mouse.current.scroll)
            {
                var scrollValue = context.ReadValue<Vector2>().normalized.y;
                if (Mathf.Abs(scrollValue) > 0)
                {
                    this.Zoom(scrollValue);
                }
            }
        }
        // - InputActionPhase.Performed ----------------------------------------------------------------------------
        else if (context.performed)
        {
            if (_lastMouseButton != null && context.action.activeControl == Mouse.current.position)
            {
                Debug.Log($"{_lastMouseButton}");
                var newMousePosition = context.ReadValue<Vector2>();
                var currentDelta = newMousePosition - _lastMousePosition;
                _dragDistance += currentDelta.magnitude;

                if (_lastMouseButton.Equals("middleButton"))
                {
                    this.Pan(currentDelta);
                }
                else if (_lastMouseButton.Equals("rightButton"))
                {
                    this.Rotate(currentDelta);
                }

                _lastMousePosition = newMousePosition;
            }
        }
        // - InputActionPhase.Canceled -----------------------------------------------------------------------------
        else
        {
            if (_lastMouseButton != null &&
                context.action.activeControl != Mouse.current.scroll)
            {
                // TODO: refactor, in the future we will have to manage also the right click (for e.g)
                if (_lastMouseButton.Equals("leftButton"))
                {
                    // If it's a finishing of a left click
                    if (_dragDistance < _dragThreshold)
                    {
                        //this.ManageRaycast();
                    }
                }

                _dragAction.Disable();

                _lastMouseButton = null;
            }
        }
    }

}
