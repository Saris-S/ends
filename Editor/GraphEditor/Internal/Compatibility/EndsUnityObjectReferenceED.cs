#if UNITY_EDITOR
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Converts temporary Unity object identifiers across supported Editor versions, persistent asset GUIDs and local IDs remain owned by the asset contract. </summary>
    internal static class EndsUnityObjectReferenceED
    {
        #region TRANSIENT REFERENCES

        /// <summary> ENDS. Encodes the complete session-local object ID as an exact JSON integer, null objects use zero and these IDs are not persistent asset identities. </summary>
        internal static EndsMcpJsonED EncodeTransientId(UnityEngine.Object asset)
        {
            if (asset == null) return EndsMcpJsonED.Num(0);

#if UNITY_6000_5_OR_NEWER
            return EndsMcpJsonED.Num(EntityId.ToULong(asset.GetEntityId()));
#else
#pragma warning disable CS0618
            return EndsMcpJsonED.Num(asset.GetInstanceID());
#pragma warning restore CS0618
#endif
        }

        /// <summary> ENDS. Resolves a nonzero numeric ID from the current Unity session, false means malformed or unassigned, true with a null object means the referenced object is unavailable. </summary>
        internal static bool TryResolveTransientId(EndsMcpJsonED identifier, out UnityEngine.Object asset)
        {
            asset = null;

            if (identifier == null || identifier.Type != EndsMcpJsonED.Kind.Number) return false;

#if UNITY_6000_5_OR_NEWER
            if (!ulong.TryParse(identifier.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong entityId) || entityId == 0) return false;

            asset = EditorUtility.EntityIdToObject(EntityId.FromULong(entityId));
#else
            if (!int.TryParse(identifier.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int instanceId) || instanceId == 0) return false;

#pragma warning disable CS0618
#if UNITY_6000_4_OR_NEWER
            // Unity 6.4 snapshots still store legacy integers, the transitional conversion resolves them to EntityId.
            asset = EditorUtility.EntityIdToObject(instanceId);
#else
            asset = EditorUtility.InstanceIDToObject(instanceId);
#endif
#pragma warning restore CS0618
#endif

            return true;
        }

        #endregion TRANSIENT REFERENCES
    }
}
#endif