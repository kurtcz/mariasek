using System;

using Microsoft.Xna.Framework;

namespace Mariasek.SharedClient.GameComponents
{
    public static class FluentExtensions
    {
        public static T Invoke<T>(this T gameComponent, Action actionHandler)
            where T : GameComponent
        {
            return gameComponent.InvokeImpl(actionHandler) as T;
        }

        public static T WaitUntil<T>(this T gameComponent, Func<bool> conditionFunc)
            where T : GameComponent
        {
            return gameComponent.WaitUntilImpl(conditionFunc) as T;
        }

        public static T Wait<T>(this T gameComponent, int milliseconds)
            where T : GameComponent
        {
            return gameComponent.WaitImpl(milliseconds) as T;
        }

        public static T MoveTo<T>(this T gameComponent, Vector2 targetPosition, float speed = 100f)
            where T : GameComponent
        {
            return gameComponent.MoveToImpl(targetPosition, speed) as T;
        }

        public static T Fade<T>(this T gameComponent, float targetOpacity, float speed = 1f)
            where T : GameComponent
        {
            return gameComponent.FadeImpl(targetOpacity, speed) as T;
        }

        public static T FadeIn<T>(this T gameComponent, float speed = 1f)
            where T : GameComponent
        {
            return gameComponent.FadeImpl(1f, speed) as T;
        }

        public static T FadeOut<T>(this T gameComponent, float speed = 1f)
            where T : GameComponent
        {
            return gameComponent.FadeImpl(0f, speed) as T;
        }

        public static T ScaleTo<T>(this T sprite, float targetScale, float speed = 1f)
            where T : Sprite
        {
            return sprite.ScaleToImpl(targetScale, speed) as T;
        }

        public static T ScaleTo<T>(this T sprite, Vector2 targetScale, float speed = 1f)
            where T : Sprite
        {
            return sprite.ScaleToImpl(targetScale, speed) as T;
        }

        public static T RotateTo<T>(this T sprite, float targetAngle, float speed = 1f)
            where T : Sprite
        {
            return sprite.RotateToImpl(targetAngle, speed) as T;
        }

        public static T Slerp<T>(this T sprite, Vector2 targetPosition, float targetAngle, float targetScale, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
            where T : Sprite
        {
            return sprite.SlerpImpl(targetPosition, targetAngle, targetScale, speed, rotationSpeed, scalingSpeed) as T;
        }
    }
}

