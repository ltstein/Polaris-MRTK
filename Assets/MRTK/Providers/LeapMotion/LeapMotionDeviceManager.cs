﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;
using System;

#if LEAPMOTIONCORE_PRESENT
using Leap;
using Leap.Unity;
using Leap.Unity.Attachments;
#endif

namespace Microsoft.MixedReality.Toolkit.LeapMotion.Input
{
    [MixedRealityDataProvider(
        typeof(IMixedRealityInputSystem),
        SupportedPlatforms.WindowsStandalone | SupportedPlatforms.WindowsEditor,
        "Leap Motion Device Manager",
        "LeapMotion/Profiles/LeapMotionDeviceManagerProfile.asset",
        "MixedRealityToolkit.Providers",
        true)]
    /// <summary>
    /// Class that detects the tracking state of leap motion hands.  This class will only run if the Leap Motion Core Assets are in the project and the Leap Motion Device
    /// Manager data provider has been added in the input system configuration profile.
    /// </summary>
    public class LeapMotionDeviceManager : BaseInputDeviceManager, IMixedRealityCapabilityCheck
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="inputSystem">The <see cref="Microsoft.MixedReality.Toolkit.Input.IMixedRealityInputSystem"/> instance that receives data from this provider.</param>
        /// <param name="name">Friendly name of the service.</param>
        /// <param name="priority">Service priority. Used to determine order of instantiation.</param>
        /// <param name="profile">The service's configuration profile.</param>
        public LeapMotionDeviceManager(
            IMixedRealityInputSystem inputSystem,
            string name = null,
            uint priority = DefaultPriority,
            BaseMixedRealityProfile profile = null) : base(inputSystem, name, priority, profile) { }


        #region IMixedRealityCapabilityCheck Implementation

        /// <inheritdoc />
        public bool CheckCapability(MixedRealityCapability capability)
        {
            // Leap Motion only supports Articulated Hands
            return (capability == MixedRealityCapability.ArticulatedHand);
        }


        #endregion IMixedRealityCapabilityCheck Implementation
#if LEAPMOTIONCORE_PRESENT

        /// <summary>
        /// The profile that contains settings for the Leap Motion Device Manager input data provider.  This profile is nested under 
        /// Input > Input Data Providers > Leap Motion Device Manager in the MixedRealityToolkit object in the hierarchy.
        /// </summary>
        public LeapMotionDeviceManagerProfile SettingsProfile => ConfigurationProfile as LeapMotionDeviceManagerProfile;

        /// <summary>
        /// The LeapServiceProvider is added to the scene at runtime in OnEnable. 
        /// </summary>
        public LeapServiceProvider LeapMotionServiceProvider { get; protected set; }

        /// <summary>
        /// The distance between the index finger tip and the thumb tip required to enter the pinch/air tap selection gesture.
        /// The pinch gesture enter will be registered for all values less than the EnterPinchDistance. The default EnterPinchDistance value is 0.02 and must be between 0.015 and 0.1. 
        /// </summary>
        private float enterPinchDistance => SettingsProfile.EnterPinchDistance;

        /// <summary>
        /// The distance between the index finger tip and the thumb tip required to exit the pinch/air tap gesture.
        /// The pinch gesture exit will be registered for all values greater than the ExitPinchDistance. The default ExitPinchDistance value is 0.05 and must be between 0.015 and 0.1. 
        /// </summary>
        private float exitPinchDistance => SettingsProfile.ExitPinchDistance;

        /// <summary>
        /// If true, the leap motion controller is connected and detected.
        /// </summary>
        private bool IsLeapConnected => LeapMotionServiceProvider.IsConnected();

        /// <summary>
        /// The Leap attachment hands, used to determine which hand is currently tracked by leap.
        /// </summary>
        private AttachmentHands leapAttachmentHands = null;

        /// <summary>
        /// List of hands that are currently in frame and detected by the leap motion controller. If there are no hands in the current frame, this list will be empty.
        /// </summary>
        private List<Hand> currentHandsDetectedByLeap => LeapMotionServiceProvider.CurrentFrame.Hands;

        // This value can only be set in the profile, the default is LeapControllerOrientation.Headset.
        private LeapControllerOrientation leapControllerOrientation => SettingsProfile.LeapControllerOrientation;

        /// <summary>
        /// Adds an offset to the game object with LeapServiceProvider attached.  This offset is only applied if the leapControllerOrientation
        /// is LeapControllerOrientation.Desk and is necessary for the hand to appear in front of the main camera. If the leap controller is on the 
        /// desk, the LeapServiceProvider is added to the scene instead of the LeapXRServiceProvider. The anchor point for the position of the leap hands is 
        /// the position of the game object with the LeapServiceProvider attached.
        /// </summary>
        private Vector3 leapHandsOffset => SettingsProfile.LeapControllerOffset;

        /// <summary>
        /// Dictionary to capture all active leap motion hands detected.
        /// </summary>
        private readonly Dictionary<Handedness, LeapMotionArticulatedHand> trackedHands = new Dictionary<Handedness, LeapMotionArticulatedHand>();

        private AttachmentHand leftAttachmentHand = null;
        private AttachmentHand rightAttachmentHand = null;

        private static readonly ProfilerMarker UpdatePerfMarker = new ProfilerMarker("[MRTK] LeapMotionDeviceManager.Update");


        [System.Serializable]
        public class Calibration
        {
            public bool alwaysActivate;
            public string name;
            public string directory;
            public bool resourceOnly;
            public string[] hmd_presence;
            public Display display;
            public Lefteye leftEye;
            public Righteye rightEye;
            public Leaptrackerodometryorigin leapTrackerOdometryOrigin;
        }
        [System.Serializable]
        public class Display
        {
            public int originX;
            public int originY;
            public int width;
            public int height;
            public int renderWidth;
            public int renderHeight;
            public int frequency;
            public float ipd;
            public float photonLatency;
        }
        [System.Serializable]
        public class Lefteye
        {
            public float ellipseMinorAxis;
            public float ellipseMajorAxis;
            public float screenForward_x;
            public float screenForward_y;
            public float screenForward_z;
            public float screenPosition_x;
            public float screenPosition_y;
            public float screenPosition_z;
            public float eyePosition_x;
            public float eyePosition_y;
            public float eyePosition_z;
            public float cameraProjection_x;
            public float cameraProjection_y;
            public float cameraProjection_z;
            public float cameraProjection_w;
            public float sphereToWorldSpace_e00;
            public float sphereToWorldSpace_e01;
            public float sphereToWorldSpace_e02;
            public float sphereToWorldSpace_e03;
            public float sphereToWorldSpace_e10;
            public float sphereToWorldSpace_e11;
            public float sphereToWorldSpace_e12;
            public float sphereToWorldSpace_e13;
            public float sphereToWorldSpace_e20;
            public float sphereToWorldSpace_e21;
            public float sphereToWorldSpace_e22;
            public float sphereToWorldSpace_e23;
            public float worldToScreenSpace_e00;
            public float worldToScreenSpace_e01;
            public float worldToScreenSpace_e02;
            public float worldToScreenSpace_e03;
            public float worldToScreenSpace_e10;
            public float worldToScreenSpace_e11;
            public float worldToScreenSpace_e12;
            public float worldToScreenSpace_e13;
            public float worldToScreenSpace_e20;
            public float worldToScreenSpace_e21;
            public float worldToScreenSpace_e22;
            public float worldToScreenSpace_e23;
        }
        [System.Serializable]
        public class Righteye
        {
            public float ellipseMinorAxis;
            public float ellipseMajorAxis;
            public float screenForward_x;
            public float screenForward_y;
            public float screenForward_z;
            public float screenPosition_x;
            public float screenPosition_y;
            public float screenPosition_z;
            public float eyePosition_x;
            public float eyePosition_y;
            public float eyePosition_z;
            public float cameraProjection_x;
            public float cameraProjection_y;
            public float cameraProjection_z;
            public float cameraProjection_w;
            public float sphereToWorldSpace_e00;
            public float sphereToWorldSpace_e01;
            public float sphereToWorldSpace_e02;
            public float sphereToWorldSpace_e03;
            public float sphereToWorldSpace_e10;
            public float sphereToWorldSpace_e11;
            public float sphereToWorldSpace_e12;
            public float sphereToWorldSpace_e13;
            public float sphereToWorldSpace_e20;
            public float sphereToWorldSpace_e21;
            public float sphereToWorldSpace_e22;
            public float sphereToWorldSpace_e23;
            public float worldToScreenSpace_e00;
            public float worldToScreenSpace_e01;
            public float worldToScreenSpace_e02;
            public float worldToScreenSpace_e03;
            public float worldToScreenSpace_e10;
            public float worldToScreenSpace_e11;
            public float worldToScreenSpace_e12;
            public float worldToScreenSpace_e13;
            public float worldToScreenSpace_e20;
            public float worldToScreenSpace_e21;
            public float worldToScreenSpace_e22;
            public float worldToScreenSpace_e23;
        }
        [System.Serializable]
        public class Leaptrackerodometryorigin
        {
            public float position_x;
            public float position_y;
            public float position_z;
            public float rotation_x;
            public float rotation_y;
            public float rotation_z;
            public float rotation_w;
        }



        /// <inheritdoc />
        public override void Enable()
        {
            base.Enable();

            if (leapControllerOrientation == LeapControllerOrientation.Headset)
            {
                // If the leap controller is mounted on a headset then add the LeapXRServiceProvider to the scene
                // The LeapXRServiceProvider can only be attached to a camera 
                LeapMotionServiceProvider = CameraCache.Main.gameObject.AddComponent<LeapXRServiceProvider>();
                //If northstar driver is installed, we need to get the LeapOffset values from the .vrsettings file
                //LeapMotionServiceProvider.GetComponent<LeapXRServiceProvider>().deviceOffsetMode = Leap.Unity.LeapXRServiceProvider.DeviceOffsetMode.Transform;
                //private Transform LeapOrigin = GetLeapOffset();
                //LeapMotionServiceProvider.GetComponent<LeapXRServiceProvider>().deviceOrigin = LeapOrigin
                //private Transform LeapOrigin = 0;
                //C:\Program Files (x86)\Steam\steamapps\common\SteamVR\drivers\northstar\resources\settings\default.vrsettings
                string path = @"c:\Program Files (x86)\Steam\steamapps\common\SteamVR\drivers\northstar\resources\settings\default.vrsettings";
                //path = "";

                if (System.IO.File.Exists(path))
                {
                    //Check if we have a valid calibration to read
                    Debug.Log(path + "exists");
                    //Create game object to apply sensor offset
                    GameObject leapProvider = new GameObject("LeapProvider");
                    //Read in data from calibration file
                    string headsetsettings = System.IO.File.ReadAllText(path);
                    Debug.Log(headsetsettings);
                    //Parse data
                    
                    Calibration headset = JsonUtility.FromJson<Calibration>(headsetsettings);

                    Leaptrackerodometryorigin leapoffset = headset.leapTrackerOdometryOrigin;

                    Debug.Log("headset : --" + headset.leapTrackerOdometryOrigin);

                    Vector3 leapposition = new Vector3(leapoffset.position_x, leapoffset.position_y, leapoffset.position_z);
                    Quaternion leaprotation = new Quaternion(leapoffset.rotation_x, leapoffset.rotation_y, leapoffset.rotation_z, leapoffset.rotation_w);
                    //Apply leap offset
                    Leap.Unity.Pose pose = new Leap.Unity.Pose(leapposition, leaprotation);
                    leapProvider.transform.SetLocalPose(pose);

                    LeapMotionServiceProvider.GetComponent<LeapXRServiceProvider>().deviceOrigin = leapProvider.transform;
                    LeapMotionServiceProvider.GetComponent<LeapXRServiceProvider>().deviceOffsetMode = Leap.Unity.LeapXRServiceProvider.DeviceOffsetMode.Transform;
                    Debug.Log("Applied Leap Offset from North Star vrsettings");
                }
                else
                {
                    Debug.Log(path + " not found, setting default Leap Offset");
                    LeapMotionServiceProvider = CameraCache.Main.gameObject.AddComponent<LeapXRServiceProvider>();
                }


            }

            if (leapControllerOrientation == LeapControllerOrientation.Desk)
            {
                // Create a separate gameobject if the leap controller is on the desk
                GameObject leapProvider = new GameObject("LeapProvider");

                // The LeapServiceProvider does not need to be attached to a camera, but the location of this gameobject is the anchor for the desk hands
                LeapMotionServiceProvider = leapProvider.AddComponent<LeapServiceProvider>();

                // Follow the transform of the main camera by adding the service provider as a child of the main camera
                leapProvider.transform.parent = CameraCache.Main.transform;

                // Apply hand position offset, an offset is required to render the hands in view and in front of the camera
                LeapMotionServiceProvider.transform.position += leapHandsOffset;
            }

            // Add the attachment hands to the scene for the purpose of getting the tracking state of each hand and joint positions
            GameObject leapAttachmentHandsGameObject = new GameObject("LeapAttachmentHands");
            leapAttachmentHands = leapAttachmentHandsGameObject.AddComponent<AttachmentHands>();

            // The first hand in attachmentHands.attachmentHands is always left
            leftAttachmentHand = leapAttachmentHands.attachmentHands[0];

            // The second hand in attachmentHands.attachmentHands is always right
            rightAttachmentHand = leapAttachmentHands.attachmentHands[1];

            // Enable all attachment point flags in the leap hand. By default, only the wrist and the palm are enabled.
            foreach (TrackedHandJoint joint in Enum.GetValues(typeof(TrackedHandJoint)))
            {
                leapAttachmentHands.attachmentPoints |= LeapMotionArticulatedHand.ConvertMRTKJointToLeapJoint(joint);
            }
        }

        /// <inheritdoc />
        public override void Disable()
        {
            base.Disable();

            // Only destroy the objects if the application is playing because the objects are added to the scene at runtime
            if (Application.isPlaying)
            {
                // Destroy AttachmentHands GameObject
                if (leapAttachmentHands != null)
                {
                    GameObject.Destroy(leapAttachmentHands.gameObject);
                }

                if (LeapMotionServiceProvider != null)
                {
                    // Destroy the LeapProvider GameObject if the controller orientation is the desk
                    if (leapControllerOrientation == LeapControllerOrientation.Desk)
                    {
                        GameObject.Destroy(LeapMotionServiceProvider.gameObject);
                    }
                    // Destroy the LeapXRServiceProvider attached to the main camera if the controller orientation is headset
                    else if (leapControllerOrientation == LeapControllerOrientation.Headset)
                    {
                        GameObject.Destroy(LeapMotionServiceProvider);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a new LeapMotionArticulatedHand to the scene.
        /// </summary>
        /// <param name="handedness">The handedness (Handedness.Left or Handedness.Right) of the hand to be added</param>
        private void OnHandDetected(Handedness handedness)
        {
            // Only create a new hand if the hand does not exist
            if (!trackedHands.ContainsKey(handedness))
            {
                var pointers = RequestPointers(SupportedControllerType.ArticulatedHand, handedness);
                var inputSource = CoreServices.InputSystem?.RequestNewGenericInputSource($"Leap {handedness} Controller", pointers, InputSourceType.Hand);
                var leapHand = new LeapMotionArticulatedHand(TrackingState.Tracked, handedness, inputSource);

                // Set pinch thresholds
                leapHand.handDefinition.EnterPinchDistance = enterPinchDistance;
                leapHand.handDefinition.ExitPinchDistance = exitPinchDistance;

                // Set the leap attachment hand to the corresponding handedness
                if (handedness == Handedness.Left)
                {
                    leapHand.SetAttachmentHands(leftAttachmentHand, LeapMotionServiceProvider);
                }
                else // handedness == Handedness.Right
                {
                    leapHand.SetAttachmentHands(rightAttachmentHand, LeapMotionServiceProvider);
                }

                // Set the pointers for an articulated hand to the leap hand
                foreach (var pointer in pointers)
                {
                    pointer.Controller = leapHand;
                }

                trackedHands.Add(handedness, leapHand);

                CoreServices.InputSystem.RaiseSourceDetected(inputSource, leapHand);
            }
        }

        /// <summary>
        /// Removes the LeapMotionArticulated hand from the scene when the tracking is lost.
        /// </summary>
        /// <param name="handedness">The handedness (Handedness.Left or Handedness.Right) of the hand to be removed</param>
        private void OnHandDetectionLost(Handedness handedness)
        {
            if (CoreServices.InputSystem != null)
            {
                CoreServices.InputSystem.RaiseSourceLost(trackedHands[handedness].InputSource);
            }

            // Disable the pointers if the hand is not tracking
            RecyclePointers(trackedHands[handedness].InputSource);

            // Remove hand from tracked hands
            trackedHands.Remove(trackedHands[handedness].ControllerHandedness);
        }

        /// <summary>
        /// Update the number of tracked leap hands.
        /// </summary>
        /// <param name="isLeftTracked">The tracking state of the left leap hand</param>
        /// <param name="isRightTracked">The tracking state of the right leap hand</param>
        private void UpdateLeapTrackedHands(bool isLeftTracked, bool isRightTracked)
        {
            // Left Hand Update
            if (isLeftTracked && !trackedHands.ContainsKey(Handedness.Left))
            {
                OnHandDetected(Handedness.Left);
            }
            else if (!isLeftTracked && trackedHands.ContainsKey(Handedness.Left))
            {
                OnHandDetectionLost(Handedness.Left);
            }

            // Right Hand Update
            if (isRightTracked && !trackedHands.ContainsKey(Handedness.Right))
            {
                OnHandDetected(Handedness.Right);
            }
            else if (!isRightTracked && trackedHands.ContainsKey(Handedness.Right))
            {
                OnHandDetectionLost(Handedness.Right);
            }
        }

        /// <inheritdoc />
        public override void Update()
        {
            base.Update();

            using (UpdatePerfMarker.Auto())
            {
                if (IsLeapConnected)
                {
                    // if the number of tracked hands in frame has changed
                    if (currentHandsDetectedByLeap.Count != trackedHands.Count)
                    {
                        UpdateLeapTrackedHands(leftAttachmentHand.isTracked, rightAttachmentHand.isTracked);
                    }

                    // Update the hand/hands that are in trackedhands
                    foreach (KeyValuePair<Handedness, LeapMotionArticulatedHand> hand in trackedHands)
                    {
                        hand.Value.UpdateState();
                    }
                }
            }
        }
#endif
    }

}

