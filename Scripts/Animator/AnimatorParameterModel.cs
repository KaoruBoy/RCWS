#if UNITY_EDITOR
using UnityEngine;

namespace RCWS
{
    public class AnimatorParameterModel
    {
        public string Name;
        public AnimatorControllerParameterType Type;
        public float DefaultFloat;
        public int DefaultInt;
        public bool DefaultBool;

        public AnimatorParameterModel() { }

        public AnimatorParameterModel(AnimatorControllerParameter parameter)
        {
            Name = parameter.name;
            Type = parameter.type;
            DefaultFloat = parameter.defaultFloat;
            DefaultInt = parameter.defaultInt;
            DefaultBool = parameter.defaultBool;
        }

        internal AnimatorControllerParameter ToUnityParameter()
        {
            return new AnimatorControllerParameter
            {
                name = Name,
                type = Type,
                defaultFloat = DefaultFloat,
                defaultInt = DefaultInt,
                defaultBool = DefaultBool
            };
        }
    }
}
#endif
