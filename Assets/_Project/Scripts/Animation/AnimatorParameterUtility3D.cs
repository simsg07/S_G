using UnityEngine;

public static class AnimatorParameterUtility3D
{
    public static bool HasParameter(Animator animator, int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}
