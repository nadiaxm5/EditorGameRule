using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameRuleEditor.Panels
{
    internal static class SettingsPrecision
    {
        private static float Round(float value) => (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static void FormatInput(FloatField field)
        {
            field.formatString = "0.##";
            field.isDelayed = true;
        }

        public static void RegisterRoundedChange(this FloatField field, System.Action<float> changed)
        {
            FormatInput(field);
            Register(field, Round, changed);
        }

        public static void RegisterRoundedChange(this Vector2Field field, System.Action<Vector2> changed)
        {
            field.Query<FloatField>().ForEach(FormatInput);
            Register(field, value => new Vector2(Round(value.x), Round(value.y)), changed);
        }

        public static void RegisterRoundedChange(this Vector3Field field, System.Action<Vector3> changed)
        {
            field.Query<FloatField>().ForEach(FormatInput);
            Register(field, value => new Vector3(Round(value.x), Round(value.y), Round(value.z)), changed);
        }

        private static void Register<T>(BaseField<T> field, Func<T, T> round, System.Action<T> changed)
        {
            field.RegisterValueChangedCallback(evt =>
            {
                T rounded = round(evt.newValue);
                if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(evt.newValue, rounded))
                    field.SetValueWithoutNotify(rounded);
                changed(rounded);
            });
        }
    }
}
