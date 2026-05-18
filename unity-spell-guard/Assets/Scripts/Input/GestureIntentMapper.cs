namespace SpellGuard.InputSystem
{
    public static class GestureIntentMapper
    {
        public static GestureAction ToMenuAction(GestureCommand command, bool allowBack)
        {
            if (!command.IsValid)
            {
                return GestureAction.None;
            }

            if (command.Kind == GestureCommandKind.Motion)
            {
                return GestureAction.FromCommand(MapMenuMotion(command.MotionGesture), command);
            }

            if (command.Kind == GestureCommandKind.StaticPose)
            {
                return GestureAction.FromCommand(MapMenuStatic(command.StaticGesture, allowBack), command);
            }

            return GestureAction.None;
        }

        public static GestureAction ToSpellAction(GestureCommand command)
        {
            if (!command.IsValid)
            {
                return GestureAction.None;
            }

            if (command.Kind == GestureCommandKind.Motion)
            {
                return GestureAction.FromCommand(MapSpellMotion(command.MotionGesture), command);
            }

            if (command.Kind == GestureCommandKind.StaticPose)
            {
                return GestureAction.FromCommand(MapSpellStatic(command.StaticGesture), command);
            }

            return GestureAction.None;
        }

        public static GestureAction ToMovementAction(GestureFrame frame)
        {
            var motion = frame.LatestMotion;
            if (motion.IsValid)
            {
                return GestureAction.FromMotion(MapMovementMotion(motion.Gesture), motion);
            }

            var primaryHand = frame.PrimaryHand;
            if (!primaryHand.IsTracked)
            {
                return GestureAction.None;
            }

            var intent = MapMovementStatic(primaryHand.StaticGesture);
            if (intent == GestureIntent.None)
            {
                return GestureAction.None;
            }

            return new GestureAction
            {
                Intent = intent,
                Confidence = primaryHand.Confidence,
                TriggeredTime = frame.Timestamp,
                SourceKind = GestureCommandKind.StaticPose,
                Handedness = primaryHand.Handedness,
                TrackId = primaryHand.TrackId
            };
        }

        public static GestureAction ToTrainingAction(GestureAction action)
        {
            switch (action.Intent)
            {
                case GestureIntent.TrainingSwipe:
                case GestureIntent.TrainingSpecialConfirm:
                    return action;
                case GestureIntent.MenuPrevious:
                case GestureIntent.MenuNext:
                case GestureIntent.MoveLeft:
                case GestureIntent.MoveRight:
                case GestureIntent.MoveForward:
                case GestureIntent.MoveBackward:
                    return CopyWithIntent(action, GestureIntent.TrainingSwipe);
                case GestureIntent.MenuConfirm:
                case GestureIntent.CastFire:
                    if (action.IsTransient)
                    {
                        return CopyWithIntent(action, GestureIntent.TrainingSpecialConfirm);
                    }
                    break;
            }

            return GestureAction.None;
        }

        public static GestureAction ToTrainingAction(MotionGestureEvent motion)
        {
            return ToTrainingAction(GestureAction.FromMotion(MapTrainingMotion(motion.Gesture), motion));
        }

        private static GestureAction CopyWithIntent(GestureAction action, GestureIntent intent)
        {
            if (!action.IsValid || intent == GestureIntent.None)
            {
                return GestureAction.None;
            }

            action.Intent = intent;
            return action;
        }

        private static GestureIntent MapMenuStatic(GestureType gesture, bool allowBack)
        {
            switch (gesture)
            {
                case GestureType.Fist:
                    return GestureIntent.MenuConfirm;
                case GestureType.OpenPalm:
                    return allowBack ? GestureIntent.MenuBack : GestureIntent.None;
                default:
                    return GestureIntent.None;
            }
        }

        private static GestureIntent MapMenuMotion(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.OpenPalmSlapLeftToRight:
                case MotionGestureType.SwipeBottomToTop:
                    return GestureIntent.MenuPrevious;
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                case MotionGestureType.SwipeTopToBottom:
                    return GestureIntent.MenuNext;
                case MotionGestureType.Snap:
                case MotionGestureType.PointToFist:
                    return GestureIntent.MenuConfirm;
                default:
                    return GestureIntent.None;
            }
        }

        private static GestureIntent MapSpellStatic(GestureType gesture)
        {
            switch (gesture)
            {
                case GestureType.Fist:
                    return GestureIntent.CastFire;
                case GestureType.VSign:
                    return GestureIntent.CastIce;
                case GestureType.OpenPalm:
                    return GestureIntent.CastShield;
                default:
                    return GestureIntent.None;
            }
        }

        private static GestureIntent MapSpellMotion(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.Snap:
                case MotionGestureType.PointToFist:
                    return GestureIntent.CastFire;
                default:
                    return GestureIntent.None;
            }
        }

        private static GestureIntent MapMovementStatic(GestureType gesture)
        {
            return gesture == GestureType.OpenPalm ? GestureIntent.MoveBackward : GestureIntent.None;
        }

        private static GestureIntent MapMovementMotion(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    return GestureIntent.MoveLeft;
                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.OpenPalmSlapLeftToRight:
                    return GestureIntent.MoveRight;
                case MotionGestureType.SwipeBottomToTop:
                    return GestureIntent.MoveForward;
                case MotionGestureType.SwipeTopToBottom:
                    return GestureIntent.MoveBackward;
                default:
                    return GestureIntent.None;
            }
        }

        private static GestureIntent MapTrainingMotion(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.SwipeBottomToTop:
                case MotionGestureType.SwipeTopToBottom:
                case MotionGestureType.OpenPalmSlapLeftToRight:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    return GestureIntent.TrainingSwipe;
                case MotionGestureType.Snap:
                case MotionGestureType.PointToFist:
                    return GestureIntent.TrainingSpecialConfirm;
                default:
                    return GestureIntent.None;
            }
        }
    }
}
