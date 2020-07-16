using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using UnityStandardAssets.Utility;

namespace UniVox.Gameplay
{
    /// <summary>
    /// This class copied and slightly modified from the UnityStandardAssets.Characters.FirstPerson.HeadBob class
    /// </summary>
    [RequireComponent(typeof(IPlayerMovementController))]
    public class HeadBob : MonoBehaviour
    {
        public Camera Camera;
        public CurveControlledBob motionBob = new CurveControlledBob();
        public LerpControlledBob jumpAndLandingBob = new LerpControlledBob();
        public GameObject playerObject;
        public float StrideInterval;
        [Range(0f, 1f)] public float RunningStrideLengthen;

        // private CameraRefocus m_CameraRefocus;
        private bool m_PreviouslyGrounded;
        private Vector3 m_OriginalCameraPosition;

        private IPlayerMovementController playerController;

        private void Start()
        {
            playerController = playerObject.GetComponent<IPlayerMovementController>();
            motionBob.Setup(Camera, StrideInterval);
            m_OriginalCameraPosition = Camera.transform.localPosition;
            //     m_CameraRefocus = new CameraRefocus(Camera, transform.root.transform, Camera.transform.localPosition);
        }


        private void Update()
        {
            //  m_CameraRefocus.GetFocusPoint();
            Vector3 newCameraPosition;
            if (playerController.Velocity.magnitude > 0 && playerController.Grounded)
            {
                Camera.transform.localPosition = motionBob.DoHeadBob(playerController.Velocity.magnitude * (playerController.Running ? RunningStrideLengthen : 1f));
                newCameraPosition = Camera.transform.localPosition;
                newCameraPosition.y = Camera.transform.localPosition.y - jumpAndLandingBob.Offset();
            }
            else
            {
                newCameraPosition = Camera.transform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - jumpAndLandingBob.Offset();
            }
            Camera.transform.localPosition = newCameraPosition;

            if (!m_PreviouslyGrounded && playerController.Grounded)
            {
                StartCoroutine(jumpAndLandingBob.DoBobCycle());
            }

            m_PreviouslyGrounded = playerController.Grounded;
            //  m_CameraRefocus.SetFocusPoint();
        }
    }
}