using UnityEngine;

namespace ChallengeShow.EditorTools
{
    internal static class ComponentUtility
    {
        /// <summary>
        /// Fetch a component, adding it if absent.
        ///
        /// Not written as <c>GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;()</c>: under Unity 6's
        /// object marshalling a missing component can come back as a non-null reference that only
        /// reports null through UnityEngine.Object's overloaded ==, so the null-coalescing operator
        /// silently keeps the dead reference and the next property set throws.
        /// </summary>
        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
