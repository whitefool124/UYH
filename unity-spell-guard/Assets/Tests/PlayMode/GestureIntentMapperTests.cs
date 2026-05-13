using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class GestureIntentMapperTests
    {
        [Test]
        public void MenuMapsDiscreteCommandsToNavigationIntents()
        {
            Assert.That(GestureIntentMapper.ToMenuAction(Motion(MotionGestureType.SwipeLeftToRight), true).Intent, Is.EqualTo(GestureIntent.MenuPrevious));
            Assert.That(GestureIntentMapper.ToMenuAction(Motion(MotionGestureType.SwipeRightToLeft), true).Intent, Is.EqualTo(GestureIntent.MenuNext));
            Assert.That(GestureIntentMapper.ToMenuAction(Motion(MotionGestureType.Snap), true).Intent, Is.EqualTo(GestureIntent.MenuConfirm));
            Assert.That(GestureIntentMapper.ToMenuAction(Static(GestureType.Fist), true).Intent, Is.EqualTo(GestureIntent.MenuConfirm));
            Assert.That(GestureIntentMapper.ToMenuAction(Static(GestureType.OpenPalm), true).Intent, Is.EqualTo(GestureIntent.MenuBack));
        }

        [Test]
        public void MovementDoesNotUsePointOrBodyShift()
        {
            Assert.That(GestureIntentMapper.ToMovementAction(Frame(GestureType.Point, MotionGestureType.None)).IsValid, Is.False);
            Assert.That(GestureIntentMapper.ToMovementAction(Frame(GestureType.None, MotionGestureType.BodyShiftLeft)).IsValid, Is.False);
            Assert.That(GestureIntentMapper.ToMovementAction(Frame(GestureType.OpenPalm, MotionGestureType.None)).Intent, Is.EqualTo(GestureIntent.MoveBackward));
            Assert.That(GestureIntentMapper.ToMovementAction(Frame(GestureType.None, MotionGestureType.SwipeBottomToTop)).Intent, Is.EqualTo(GestureIntent.MoveForward));
        }

        [Test]
        public void SpellAndTrainingUseIntentSemantics()
        {
            var snapAction = GestureIntentMapper.ToSpellAction(Motion(MotionGestureType.Snap));
            var swipeTraining = GestureIntentMapper.ToTrainingAction(new GestureAction
            {
                Intent = GestureIntent.MenuNext,
                Confidence = 1f,
                TriggeredTime = Time.time,
                SourceKind = GestureCommandKind.Motion,
                Handedness = GestureHandedness.Unknown,
                TrackId = -1
            });

            Assert.That(GestureIntentMapper.ToSpellAction(Static(GestureType.Fist)).Intent, Is.EqualTo(GestureIntent.CastFire));
            Assert.That(GestureIntentMapper.ToSpellAction(Static(GestureType.VSign)).Intent, Is.EqualTo(GestureIntent.CastIce));
            Assert.That(GestureIntentMapper.ToSpellAction(Static(GestureType.OpenPalm)).Intent, Is.EqualTo(GestureIntent.CastShield));
            Assert.That(snapAction.Intent, Is.EqualTo(GestureIntent.CastFire));
            Assert.That(swipeTraining.Intent, Is.EqualTo(GestureIntent.TrainingSwipe));
            Assert.That(GestureIntentMapper.ToTrainingAction(snapAction).Intent, Is.EqualTo(GestureIntent.TrainingSpecialConfirm));
        }

        private static GestureCommand Static(GestureType gesture)
        {
            return new GestureCommand
            {
                Kind = GestureCommandKind.StaticPose,
                StaticGesture = gesture,
                MotionGesture = MotionGestureType.None,
                Confidence = 1f,
                TriggeredTime = Time.time,
                Handedness = GestureHandedness.Unknown,
                TrackId = -1
            };
        }

        private static GestureCommand Motion(MotionGestureType gesture)
        {
            return GestureCommand.FromMotion(new MotionGestureEvent
            {
                Gesture = gesture,
                Confidence = 1f,
                TriggeredTime = Time.time
            });
        }

        private static GestureFrame Frame(GestureType staticGesture, MotionGestureType motionGesture)
        {
            return new GestureFrame
            {
                FrameId = 1,
                Timestamp = Time.time,
                Source = GestureSourceKind.Mock,
                Hands = staticGesture == GestureType.None
                    ? System.Array.Empty<TrackedHandState>()
                    : new[]
                    {
                        new TrackedHandState
                        {
                            TrackId = 1,
                            Handedness = GestureHandedness.Right,
                            IsTracked = true,
                            StaticGesture = staticGesture,
                            Confidence = 1f,
                            ViewportPosition = new Vector2(0.5f, 0.5f),
                            PalmCenter = new Vector2(0.5f, 0.5f),
                            Landmarks = System.Array.Empty<Vector2>()
                        }
                    },
                LatestMotion = motionGesture == MotionGestureType.None
                    ? MotionGestureEvent.None
                    : new MotionGestureEvent
                    {
                        Gesture = motionGesture,
                        Confidence = 1f,
                        TriggeredTime = Time.time
                    }
            };
        }
    }
}
