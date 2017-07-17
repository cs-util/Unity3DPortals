using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class PortalCamera : MonoBehaviour {
        Camera _parent;
        Camera _camera;
        Portal _portal;
        Scene _enterScene;
        Scene _exitScene;
        int _renderDepth;
        private int _framesSinceLastUse = 0;
        private Material _depthPunchMaterial;
        private FrameData _previousFrameData;
        public Matrix4x4 lastFrameWorldToCameraMatrix;
        public Matrix4x4 lastFrameProjectionMatrix;
        public RenderTexture lastFrameRenderTexture;

        public static Dictionary<Camera, PortalCamera> cameraMap = new Dictionary<Camera, PortalCamera>();

        public static PortalCamera current {
            get {
                PortalCamera c = null;
                cameraMap.TryGetValue(Camera.current, out c);
                return c;
            }
        }

        public FrameData PreviousFrameData {
            get { return _previousFrameData; }
        }

        public Camera parent {
            get { return _parent; }
            set { _parent = value; }
        }

        public new Camera camera {
            get { return _camera; }
        }


        public Portal portal {
            get { return _portal; }
            set { _portal = value; }
        }

        public Scene enterScene {
            get { return _enterScene; }
            set { _enterScene = value; }
        }

        public Scene exitScene {
            get {
                return _exitScene;
            }
            set {
                _exitScene = value;

                Scene activeScene = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(_exitScene);
                _sceneRenderSettings.CopyFromGlobalRenderSettings();
                SceneManager.SetActiveScene(activeScene);
            }
        }

        public int renderDepth {
            get {
                return _renderDepth;
            }
            set {
                _renderDepth = value;
            }
        }

        // RenderSettingsStruct _savedRenderSettings = new RenderSettingsStruct();
        RenderSettingsStruct _sceneRenderSettings = new RenderSettingsStruct();

        public void Awake() {
            _camera = GetComponent<Camera>();
            cameraMap[_camera] = this;

            _previousFrameData = new FrameData();
        }

        private void OnDestroy() {
            if (cameraMap != null && cameraMap.ContainsKey(_camera)) {
                cameraMap.Remove(_camera);
            }

            if (_camera && _camera.targetTexture && _camera.targetTexture != _previousFrameData.leftEyeTexture && _camera.targetTexture != _previousFrameData.rightEyeTexture) {
                RenderTexture.ReleaseTemporary(_camera.targetTexture);
            }
            if (_previousFrameData.leftEyeTexture) {
                RenderTexture.ReleaseTemporary(_previousFrameData.leftEyeTexture);
            }
            if (_previousFrameData.rightEyeTexture) {
                RenderTexture.ReleaseTemporary(_previousFrameData.rightEyeTexture);
            }
        }

        void Update() {
            if (_framesSinceLastUse > 0) {
                Util.SafeDestroy(this.gameObject);
            }
            _framesSinceLastUse++;
        }

        Vector3 GetStereoPosition(Camera camera, VRNode node) {
            if (camera.stereoEnabled) {
                // If our parent is rendering stereo, we need to handle eye offsets and root transformations
                Vector3 localPosition = InputTracking.GetLocalPosition(node);

                // GetLocalPosition returns the local position of the camera, but we need the global position
                // so we have to manually grab the parent.
                Transform parent = camera.transform.parent;
                if (parent) {
                    return parent.TransformPoint(localPosition);
                } else {
                    return localPosition;
                }
            } else {
                // Otherwise, we can just return the camera's position
                return camera.transform.position;
            }
        }

        Quaternion GetStereoRotation(Camera camera, VRNode node) {
            if (camera.stereoEnabled) {
                Quaternion localRotation = InputTracking.GetLocalRotation(node);
                Transform parent = camera.transform.parent;
                if (parent) {
                    return parent.rotation * localRotation;
                } else {
                    return localRotation;
                }
            } else {
                // Otherwise, we can just return the camera's position
                return camera.transform.rotation;
            }
        }

        private void SaveFrameData() {
            switch (_camera.stereoTargetEye) {
                case StereoTargetEyeMask.Right:
                    if (_previousFrameData.rightEyeTexture) {
                        RenderTexture.ReleaseTemporary(_previousFrameData.rightEyeTexture);
                    }
                    _previousFrameData.rightEyeProjectionMatrix = _camera.projectionMatrix;
                    _previousFrameData.rightEyeWorldToCameraMatrix = _camera.worldToCameraMatrix;
                    _previousFrameData.rightEyeTexture = _camera.targetTexture;
                    break;
                case StereoTargetEyeMask.Left:
                case StereoTargetEyeMask.Both:
                case StereoTargetEyeMask.None:
                default:
                    if (_previousFrameData.leftEyeTexture) {
                        RenderTexture.ReleaseTemporary(_previousFrameData.leftEyeTexture);
                    }
                    _previousFrameData.leftEyeProjectionMatrix = _camera.projectionMatrix;
                    _previousFrameData.leftEyeWorldToCameraMatrix = _camera.worldToCameraMatrix;
                    _previousFrameData.leftEyeTexture = _camera.targetTexture;
                    break;
            }
        }

        public RenderTexture RenderToTexture(Camera.MonoOrStereoscopicEye eye, bool renderBackface) {
            _framesSinceLastUse = 0;

            // Copy parent camera's settings
            CopyCameraSettings(_parent, _camera);

            Vector3 parentEyePosition = Vector3.zero;
            Quaternion parentEyeRotation = Quaternion.identity;

            Matrix4x4 projectionMatrix = _parent.projectionMatrix;
            switch (eye) {
                case Camera.MonoOrStereoscopicEye.Left:
                    _camera.stereoTargetEye = StereoTargetEyeMask.Left;
                    projectionMatrix = _parent.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                    parentEyePosition = GetStereoPosition(_parent, VRNode.LeftEye);
                    parentEyeRotation = GetStereoRotation(_parent, VRNode.LeftEye);
                    break;
                case Camera.MonoOrStereoscopicEye.Right:
                    _camera.stereoTargetEye = StereoTargetEyeMask.Right;
                    projectionMatrix = _parent.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                    parentEyePosition = GetStereoPosition(_parent, VRNode.RightEye);
                    parentEyeRotation = GetStereoRotation(_parent, VRNode.RightEye);
                    break;
                case Camera.MonoOrStereoscopicEye.Mono:
                default:
                    _camera.stereoTargetEye = _parent.stereoTargetEye;
                    projectionMatrix = _parent.projectionMatrix;
                    parentEyePosition = _parent.transform.position;
                    parentEyeRotation = _parent.transform.rotation;
                    break;
            }

            _camera.transform.position = _portal.TeleportPoint(parentEyePosition);
            _camera.transform.rotation = _portal.TeleportRotation(parentEyeRotation);
            _camera.projectionMatrix = projectionMatrix;

            //if (_portal.UseProjectionMatrix) {
            //    _camera.projectionMatrix = CalculateProjectionMatrix(eye);
            //} else {
            //    _camera.ResetProjectionMatrix();
            //}

            //if (_portal.UseCullingMatrix) {
            //    _camera.cullingMatrix = CalculateCullingMatrix();
            //} else {
            //    _camera.ResetCullingMatrix();
            //}

            //if (_portal.UseDepthMask) {
            //    DrawDepthPunchMesh(renderBackface);
            //} else {
            //    _camera.RemoveAllCommandBuffers();
            //}

            RenderTexture texture = RenderTexture.GetTemporary(_parent.pixelWidth, _parent.pixelHeight, 24, RenderTextureFormat.Default);
            _camera.targetTexture = texture;
            _camera.Render();

            SaveFrameData();

            return texture;
        }

        private void DrawDepthPunchMesh(bool renderBackface) {
            if (!_depthPunchMaterial) {
                Shader shader = Shader.Find("Portals/DepthPunch");
                _depthPunchMaterial = new Material(shader);
            }

            // Use a command buffer that clears depth to 0 (infinitely close)
            if (_camera.commandBufferCount == 0) {
                CommandBuffer buf = new CommandBuffer();
                buf.name = "Set Depth to 0";
                buf.ClearRenderTarget(true, false, Color.red, 0.0f);
                _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, buf);
            }

            // Need to render from the parent's point of view
            PortalRenderer renderer = _portal.PortalRenderer;
            Matrix4x4 matrix = _camera.cameraToWorldMatrix * _parent.worldToCameraMatrix * renderer.transform.localToWorldMatrix;

            // Punch a hole in the depth buffer, so that everything can be drawn behind it
            Graphics.DrawMesh(PortalRenderer.Mesh, matrix, _depthPunchMaterial, renderer.gameObject.layer, _camera, 0);
            if (renderBackface) {
                Graphics.DrawMesh(PortalRenderer.Mesh, matrix, _depthPunchMaterial, renderer.gameObject.layer, _camera, 1);
            }
        }

        void CopyCameraSettings(Camera src, Camera dst) {
            dst.clearFlags = src.clearFlags;
            dst.backgroundColor = src.backgroundColor;
            dst.farClipPlane = src.farClipPlane;
            dst.nearClipPlane = src.nearClipPlane;
            dst.orthographic = src.orthographic;
            dst.aspect = src.aspect;
            dst.orthographicSize = src.orthographicSize;
            dst.renderingPath = src.renderingPath;
            dst.allowHDR = src.allowHDR;
            dst.allowMSAA = src.allowMSAA;
            dst.cullingMask = src.cullingMask;
            //dst.depthTextureMode = src.depthTextureMode;
            //dst.transparencySortAxis = src.transparencySortAxis;
            //dst.transparencySortMode = src.transparencySortMode;
            // TODO: Fix occlusion culling
            dst.useOcclusionCulling = src.useOcclusionCulling;

            dst.eventMask = 0; // Ignore OnMouseXXX events

            if (!dst.stereoEnabled) {
                // Can't set FoV while in VR
                dst.fieldOfView = src.fieldOfView;
            }
        }

        //void DecomposeMatrix4x4(Matrix4x4 matrix) {
        //    float near = matrix.m23 / (matrix.m22 - 1);
        //    float far = matrix.m23 / (matrix.m22 + 1);
        //    float bottom = near * (matrix.m12 - 1) / matrix.m11;
        //    float top = near * (matrix.m12 + 1) / matrix.m11;
        //    float left = near * (matrix.m02 - 1) / matrix.m00;
        //    float right = near * (matrix.m02 + 1) / matrix.m00;

        //    Debug.Log("near: " + near);
        //    Debug.Log("far: " + far);
        //    Debug.Log("bottom: " + bottom);
        //    Debug.Log("top: " + top);
        //    Debug.Log("left: " + left);
        //    Debug.Log("right: " + right);
        //}

        Matrix4x4 CalculateProjectionMatrix(Camera.MonoOrStereoscopicEye eye) {
            // Set targetTexture to null because GetStereoProjectionMatrix won't return a valid matrix otherwise.
            RenderTexture savedTargetTexture = _camera.targetTexture;
            _camera.targetTexture = null;

            // Restore original projection matrix
            _camera.ResetProjectionMatrix();

            Matrix4x4 projectionMatrix;
            switch (eye) {
                case Camera.MonoOrStereoscopicEye.Left:
                    projectionMatrix = _camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                    break;
                case Camera.MonoOrStereoscopicEye.Right:
                    projectionMatrix = _camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                    break;
                case Camera.MonoOrStereoscopicEye.Mono:
                default:
                    projectionMatrix = _camera.projectionMatrix;
                    break;
            }
            _camera.targetTexture = savedTargetTexture;

            // Calculate plane made from the exit portal's trnasform
            Plane exitPortalPlane = _portal.ExitPortal.Plane;

            // Determine whether or not we've crossed the plane already. If we have, we don't need to apply
            // oblique frustum clipping. Offset the value by our portal's ClippingOffset to reduce the effects
            // so that it swaps over slightly early. This helps reduce artifacts caused by loss of depth accuracy.
            bool onFarSide = !new Plane(exitPortalPlane.normal, exitPortalPlane.distance - _portal.ClippingOffset).GetSide(transform.position);
            if (onFarSide) {
                return projectionMatrix;
            }

            // Offset the clipping plane itself so that a character walking through a portal has no seams
            exitPortalPlane.distance -= _portal.ClippingOffset / 2;

            // Project our world space clipping plane to the camera's local coordinates
            // e.g. normal (0, 0, 1) becomes (1, 0, 0) if we're looking left parallel to the plane
            Vector4 cameraSpaceNormal = _camera.transform.InverseTransformDirection(exitPortalPlane.normal);
            Vector4 cameraSpacePoint = _camera.transform.InverseTransformPoint(exitPortalPlane.normal * -exitPortalPlane.distance);

            // Calculate the d value for our plane by projecting our transformed point
            // onto our transformed normal vector.
            float distanceFromPlane = Vector4.Dot(cameraSpaceNormal, cameraSpacePoint);

            // Not sure why x and y have to be negative.
            Vector4 cameraSpacePlane = new Vector4(-cameraSpaceNormal.x, -cameraSpaceNormal.y, cameraSpaceNormal.z, distanceFromPlane);

            //DecomposeMatrix4x4(projectionMatrix);
            //Valve.VR.EVREye evrEye = eye == Camera.MonoOrStereoscopicEye.Left ? Valve.VR.EVREye.Eye_Left : Valve.VR.EVREye.Eye_Right;
            //projectionMatrix = HMDMatrix4x4ToMatrix4x4(SteamVR.instance.hmd.GetProjectionMatrix(Valve.VR.EVREye.Eye_Left, _camera.nearClipPlane, _camera.farClipPlane, Valve.VR.EGraphicsAPIConvention.API_DirectX));

            //cameraSpacePlane = new Vector4(1, 0, -1, -3);

            MakeProjectionMatrixOblique(ref projectionMatrix, cameraSpacePlane);
            return projectionMatrix;
        }

        void MakeProjectionMatrixOblique(ref Matrix4x4 projection, Vector4 clipPlane) {
            // Source: http://aras-p.info/texts/obliqueortho.html
            Vector4 q = projection.inverse * new Vector4(
                Mathf.Sign(clipPlane.x),
                Mathf.Sign(clipPlane.y),
                1.0f,
                1.0f
            );
            Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
            // third row = clip plane - fourth row
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];
        }

        //Matrix4x4 HMDMatrix4x4ToMatrix4x4(Valve.VR.HmdMatrix44_t input) {
        //    var m = Matrix4x4.identity;

        //    m[0, 0] = input.m0;
        //    m[0, 1] = input.m1;
        //    m[0, 2] = input.m2;
        //    m[0, 3] = input.m3;

        //    m[1, 0] = input.m4;
        //    m[1, 1] = input.m5;
        //    m[1, 2] = input.m6;
        //    m[1, 3] = input.m7;

        //    m[2, 0] = input.m8;
        //    m[2, 1] = input.m9;
        //    m[2, 2] = input.m10;
        //    m[2, 3] = input.m11;

        //    m[3, 0] = input.m12;
        //    m[3, 1] = input.m13;
        //    m[3, 2] = input.m14;
        //    m[3, 3] = input.m15;

        //    return m;
        //}

        Matrix4x4 CalculateCullingMatrix() {
            _camera.ResetCullingMatrix();

            // Lower left of the backside of our plane in world coordinates
            Vector3 pa = _portal.ExitPortal.transform.TransformPoint(new Vector3(0.5f, -0.5f, 0));

            // Lower right
            Vector3 pb = _portal.ExitPortal.transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0));

            // Upper left
            Vector3 pc = _portal.ExitPortal.transform.TransformPoint(new Vector3(0.5f, 0.5f, 0));

            Vector3 pe = _camera.transform.position;

            // Calculate what our horizontal field of view would be with off-axis projection.
            // If this fov is greater than our camera's fov, we should just use the camera's default projection
            // matrix instead. Otherwise, the frustum's fov will approach 180 degrees (way too large).
            Vector3 camToLowerLeft = pa - _camera.transform.position;
            camToLowerLeft.y = 0;
            Vector3 camToLowerRight = pb - _camera.transform.position;
            camToLowerRight.y = 0;
            float fieldOfView = Vector3.Angle(camToLowerLeft, camToLowerRight);
            if (fieldOfView > _camera.fieldOfView) {
                return _camera.cullingMatrix;
            } else {
                return CalculateOffAxisProjectionMatrix(_camera, pa, pb, pc, pe);
            }
        }

        Matrix4x4 CalculateOffAxisProjectionMatrix(Camera camera, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 pe) {
            // eye position
            float n = camera.nearClipPlane;
            // distance of near clipping plane
            float f = camera.farClipPlane;
            // distance of far clipping plane

            Vector3 va; // from pe to pa
            Vector3 vb; // from pe to pb
            Vector3 vc; // from pe to pc
            Vector3 vr; // right axis of screen
            Vector3 vu; // up axis of screen
            Vector3 vn; // normal vector of screen

            float l; // distance to left screen edge
            float r; // distance to right screen edge
            float b; // distance to bottom screen edge
            float t; // distance to top screen edge
            float d; // distance from eye to screen 

            vr = pb - pa;
            vu = pc - pa;
            va = pa - pe;
            vb = pb - pe;
            vc = pc - pe;

            // are we looking at the backface of the plane object?
            if (Vector3.Dot(-Vector3.Cross(va, vc), vb) < 0.0) {
                // mirror points along the z axis (most users 
                // probably expect the x axis to stay fixed)
                vu = -vu;
                pa = pc;
                pb = pa + vr;
                pc = pa + vu;
                va = pa - pe;
                vb = pb - pe;
                vc = pc - pe;
            }

            vr.Normalize();
            vu.Normalize();
            vn = -Vector3.Cross(vr, vu);
            // we need the minus sign because Unity 
            // uses a left-handed coordinate system
            vn.Normalize();

            d = -Vector3.Dot(va, vn);

            // Set near clip plane
            n = d; // + _clippingDistance;
            //camera.nearClipPlane = n;

            l = Vector3.Dot(vr, va) * n / d;
            r = Vector3.Dot(vr, vb) * n / d;
            b = Vector3.Dot(vu, va) * n / d;
            t = Vector3.Dot(vu, vc) * n / d;

            Matrix4x4 p = new Matrix4x4(); // projection matrix 
            p[0, 0] = 2.0f * n / (r - l);
            p[0, 1] = 0.0f;
            p[0, 2] = (r + l) / (r - l);
            p[0, 3] = 0.0f;

            p[1, 0] = 0.0f;
            p[1, 1] = 2.0f * n / (t - b);
            p[1, 2] = (t + b) / (t - b);
            p[1, 3] = 0.0f;

            p[2, 0] = 0.0f;
            p[2, 1] = 0.0f;
            p[2, 2] = (f + n) / (n - f);
            p[2, 3] = 2.0f * f * n / (n - f);

            p[3, 0] = 0.0f;
            p[3, 1] = 0.0f;
            p[3, 2] = -1.0f;
            p[3, 3] = 0.0f;

            Matrix4x4 rm = new Matrix4x4(); // rotation matrix;
            rm[0, 0] = vr.x;
            rm[0, 1] = vr.y;
            rm[0, 2] = vr.z;
            rm[0, 3] = 0.0f;

            rm[1, 0] = vu.x;
            rm[1, 1] = vu.y;
            rm[1, 2] = vu.z;
            rm[1, 3] = 0.0f;

            rm[2, 0] = vn.x;
            rm[2, 1] = vn.y;
            rm[2, 2] = vn.z;
            rm[2, 3] = 0.0f;

            rm[3, 0] = 0.0f;
            rm[3, 1] = 0.0f;
            rm[3, 2] = 0.0f;
            rm[3, 3] = 1.0f;

            Matrix4x4 tm = new Matrix4x4(); // translation matrix;
            tm[0, 0] = 1.0f;
            tm[0, 1] = 0.0f;
            tm[0, 2] = 0.0f;
            tm[0, 3] = -pe.x;

            tm[1, 0] = 0.0f;
            tm[1, 1] = 1.0f;
            tm[1, 2] = 0.0f;
            tm[1, 3] = -pe.y;

            tm[2, 0] = 0.0f;
            tm[2, 1] = 0.0f;
            tm[2, 2] = 1.0f;
            tm[2, 3] = -pe.z;

            tm[3, 0] = 0.0f;
            tm[3, 1] = 0.0f;
            tm[3, 2] = 0.0f;
            tm[3, 3] = 1.0f;

            Matrix4x4 worldToCameraMatrix = rm * tm;
            return p * worldToCameraMatrix;
        }



        //public CameraEvent cameraEvent;
        //void OnValidate() {
        //    _camera.RemoveAllCommandBuffers();
        //    CommandBuffer buf = new CommandBuffer();
        //    buf.SetGlobalMatrix("UNITY_MATRIX_VP", Matrix4x4.identity);
        //    buf.SetGlobalMatrix("UNITY_MATRIX_MVP", Matrix4x4.identity);
        //    _camera.AddCommandBuffer(cameraEvent, buf);

        //}

        //void OnPreRender() {
        //    //if (!copyGI ||
        //    //    !enterScene.isLoaded || !enterScene.IsValid() ||
        //    //    !exitScene.isLoaded || !exitScene.IsValid() ||
        //    //    enterScene == exitScene) {
        //    //    return;
        //    //}

        //    //_savedRenderSettings.CopyFromGlobalRenderSettings();
        //    //_sceneRenderSettings.CopyToGlobalRenderSettings();
        //    //if (renderDepth == 1) {
        //    //    RenderSettings.ambientSkyColor = Color.red;
        //    //} else if (renderDepth == 2) {
        //    //    RenderSettings.ambientSkyColor = Color.green;
        //    //} else if (renderDepth == 3) {
        //    //    RenderSettings.ambientSkyColor = Color.blue;
        //    //}
        //}

        //void OnPostRender() {
        //    //if (!copyGI ||
        //    //    !enterScene.isLoaded || !enterScene.IsValid() ||
        //    //    !exitScene.isLoaded || !exitScene.IsValid() ||
        //    //    enterScene == exitScene) {
        //    //    return;
        //    //}
        //    //_savedRenderSettings.CopyToGlobalRenderSettings();

        //    //RenderSettings.ambientSkyColor = Color.white;
        //}

        Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3) { //get the intersection point of 3 planes
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }

        void DrawFrustumGizmo(Matrix4x4 matrix) {
            Vector3[] nearCorners = new Vector3[4]; //Approx'd nearplane corners
            Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
            Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(matrix); //get planes from matrix
            Plane temp = camPlanes[1]; camPlanes[1] = camPlanes[2]; camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop

            for (int i = 0; i < 4; i++) {
                nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
                farCorners[i] = Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix
            }
            for (int i = 0; i < 4; i++) {
                Gizmos.DrawLine(nearCorners[i], nearCorners[(i + 1) % 4]); //near corners on the created projection matrix
                Gizmos.DrawLine(farCorners[i], farCorners[(i + 1) % 4]); //far corners on the created projection matrix
                Gizmos.DrawLine(nearCorners[i], farCorners[i]); //sides of the created projection matrix
            }
        }

        void OnDrawGizmosSelected() {
            Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
            DrawFrustumGizmo(_camera.cullingMatrix);

            Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
            DrawFrustumGizmo(_camera.projectionMatrix * _camera.worldToCameraMatrix);
        }

        struct RenderSettingsStruct {
            public Color ambientEquatorColor;
            public Color ambientGroundColor;
            public float ambientIntensity;
            public Color ambientLight;
            public AmbientMode ambientMode;
            public SphericalHarmonicsL2 ambientProbe;
            public Color ambientSkyColor;
            public Cubemap customReflection;
            public DefaultReflectionMode defaultReflectionMode;
            public int defaultReflectionResolution;
            public float flareFadeSpeed;
            public float flareStrength;
            public bool fog;
            public Color fogColor;
            public float fogDensity;
            public float fogEndDistance;
            public FogMode fogMode;
            public float fogStartDistance;
            public float haloStrength;
            public int reflectionBounces;
            public float reflectionIntensity;
            public Material skybox;
            public Light sun;

            public void CopyFromGlobalRenderSettings() {
                ambientEquatorColor = RenderSettings.ambientEquatorColor;
                ambientGroundColor = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                ambientLight = RenderSettings.ambientLight;
                ambientMode = RenderSettings.ambientMode;
                ambientProbe = RenderSettings.ambientProbe;
                ambientSkyColor = RenderSettings.ambientSkyColor;
                customReflection = RenderSettings.customReflection;
                defaultReflectionMode = RenderSettings.defaultReflectionMode;
                defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
                flareFadeSpeed = RenderSettings.flareFadeSpeed;
                flareStrength = RenderSettings.flareStrength;
                fog = RenderSettings.fog;
                fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity;
                fogEndDistance = RenderSettings.fogEndDistance;
                fogMode = RenderSettings.fogMode;
                fogStartDistance = RenderSettings.fogStartDistance;
                haloStrength = RenderSettings.haloStrength;
                reflectionBounces = RenderSettings.reflectionBounces;
                reflectionIntensity = RenderSettings.reflectionIntensity;
                skybox = RenderSettings.skybox;
                sun = RenderSettings.sun;
            }

            public void CopyToGlobalRenderSettings() {
                // Bug workaround where reflections were being sampled from the wrong skybox
                //RenderSettings.skybox = null;
                RenderSettings.skybox = skybox;

                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientProbe = ambientProbe;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.customReflection = customReflection;
                RenderSettings.defaultReflectionMode = defaultReflectionMode;
                RenderSettings.defaultReflectionResolution = defaultReflectionResolution;
                RenderSettings.flareFadeSpeed = flareFadeSpeed;
                RenderSettings.flareStrength = flareStrength;
                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogEndDistance = fogEndDistance;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.haloStrength = haloStrength;
                RenderSettings.reflectionBounces = reflectionBounces;
                RenderSettings.reflectionIntensity = reflectionIntensity;

                RenderSettings.sun = sun;
                //DynamicGI.UpdateEnvironment();
            }
        }

        [System.Serializable] // TODO: shouldn't be serializable in the future
        public class FrameData {
            public RenderTexture leftEyeTexture;
            public RenderTexture rightEyeTexture;

            public Matrix4x4 leftEyeProjectionMatrix;
            public Matrix4x4 rightEyeProjectionMatrix;

            public Matrix4x4 leftEyeWorldToCameraMatrix;
            public Matrix4x4 rightEyeWorldToCameraMatrix;

            public FrameData() { }
        }
    }
}
