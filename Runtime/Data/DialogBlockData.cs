using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary> ENDS. Preserves the identity of one editable list block through snapshots, saving and Undo, changing its content does not replace the block. </summary>
    [Serializable]
    public abstract class DialogBlockData
    {
        [FormerlySerializedAs(nameof(BlockId))][HideInInspector][SerializeField] private string _blockId;

        /// <summary> ENDS. Stores a hidden readable identity unique within its dialog, CH, CND, CNS and ANM numbers survive edits and moves while copies receive new identities. </summary>
        public string BlockId { get => _blockId; set => _blockId = value; }
    }
}
