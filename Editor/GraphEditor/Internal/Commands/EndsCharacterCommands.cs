#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsCharacterCommands
    {
        #region CHARACTER

        internal static CharacterGroup Create(DialogDatabase database, string name, bool rejectExisting)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (database.CharacterGroups == null) database.CharacterGroups = new List<CharacterGroup>();
            if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Character name is empty.");

            CharacterGroup existing = Find(database, name);

            if (existing != null)
            {
                if (rejectExisting) throw new InvalidOperationException("Character already exists.");

                return existing;
            }

            CharacterGroup group = new CharacterGroup();
            group.characterName = name;
            group.sprites = new List<CharacterSprite>();
            database.CharacterGroups.Add(group);

            return group;
        }

        internal static int Rename(DialogDatabase database, string oldName, string newName)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");

            newName = newName?.Trim() ?? "";

            if (string.IsNullOrEmpty(newName)) throw new InvalidOperationException("Character name cannot be empty.");

            CharacterGroup group = Find(database, oldName);

            if (group == null) group = Find(database, newName);
            if (group == null) throw new InvalidOperationException("Character not found.");
            if (group.characterName == oldName && oldName != newName)
            {
                if (Find(database, newName) != null) throw new InvalidOperationException("Character '" + newName + "' already exists.");

                group.characterName = newName;
            }

            return DialogReferenceRename.RenameCharacterActorInDialogs(database, oldName, newName);
        }

        internal static void MarkActionsMissing(DialogDatabase database, string name)
        {
            if (database == null || string.IsNullOrEmpty(name)) return;

            DialogReferenceCleanup.MarkCharacterActionsMissingInDialogs(database, name);
        }

        internal static void RemoveAt(DialogDatabase database, int index)
        {
            if (database?.CharacterGroups == null) return;
            if (index < 0 || index >= database.CharacterGroups.Count) return;

            database.CharacterGroups.RemoveAt(index);
        }

        internal static void Delete(DialogDatabase database, string name, bool markLineNames)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Character name is empty.");

            MarkActionsMissing(database, name);

            if (markLineNames) DialogReferenceCleanup.MarkLineCharacterNameMissingInDialogs(database, name);

            if (database.CharacterGroups == null) return;

            for (int i = database.CharacterGroups.Count - 1; i >= 0; i--)
            {
                CharacterGroup group = database.CharacterGroups[i];

                if (group != null && group.characterName == name) database.CharacterGroups.RemoveAt(i);
            }
        }

        #endregion CHARACTER

        #region SPRITE

        internal static CharacterSprite AddSprite(CharacterGroup group, string spriteName)
        {
            if (group == null) throw new InvalidOperationException("Character not found.");
            if (group.sprites == null) group.sprites = new List<CharacterSprite>();
            if (string.IsNullOrEmpty(spriteName)) throw new InvalidOperationException("Sprite slot is empty.");

            CharacterSprite existing = FindSprite(group, spriteName);

            if (existing != null) return existing;

            CharacterSprite sprite = new CharacterSprite();
            sprite.spriteName = spriteName;
            group.sprites.Add(sprite);

            return sprite;
        }

        internal static void CopySpriteNames(CharacterGroup source, CharacterGroup target)
        {
            if (source?.sprites == null || target == null) return;
            if (target.sprites == null) target.sprites = new List<CharacterSprite>();

            for (int i = 0; i < source.sprites.Count; i++)
            {
                CharacterSprite sprite = source.sprites[i];

                if (sprite == null || string.IsNullOrEmpty(sprite.spriteName)) continue;
                if (FindSprite(target, sprite.spriteName) != null) continue;

                target.sprites.Add(new CharacterSprite { spriteName = sprite.spriteName });
            }
        }

        internal static int RenameSprite(DialogDatabase database, string characterName, string oldName, string newName)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");

            newName = newName?.Trim() ?? "";

            if (string.IsNullOrEmpty(newName)) throw new InvalidOperationException("Emotion name cannot be empty.");

            CharacterGroup group = Find(database, characterName);

            if (group == null) throw new InvalidOperationException("Character not found.");

            CharacterSprite sprite = FindSprite(group, oldName);

            if (sprite == null) sprite = FindSprite(group, newName);
            if (sprite == null) throw new InvalidOperationException("Sprite not found.");
            if (sprite.spriteName == oldName && oldName != newName)
            {
                if (FindSprite(group, newName) != null) throw new InvalidOperationException("Emotion '" + newName + "' already exists for this character.");

                sprite.spriteName = newName;
            }

            return DialogReferenceRename.RenameSpriteInDialogs(database, characterName, oldName, newName);
        }

        internal static void MarkSpriteMissing(DialogDatabase database, string characterName, string emotionName)
        {
            if (database == null || string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(emotionName)) return;

            DialogReferenceCleanup.MarkSpriteMissingInDialogs(database, characterName, emotionName);
        }

        internal static void DeleteSprite(DialogDatabase database, string characterName, string emotionName)
        {
            MarkSpriteMissing(database, characterName, emotionName);

            CharacterGroup group = Find(database, characterName);

            if (group?.sprites == null) return;

            for (int i = group.sprites.Count - 1; i >= 0; i--)
            {
                CharacterSprite sprite = group.sprites[i];

                if (sprite != null && sprite.spriteName == emotionName) group.sprites.RemoveAt(i);
            }
        }

        #endregion SPRITE

        #region LOOKUP

        internal static CharacterGroup Find(DialogDatabase database, string name)
        {
            if (database?.CharacterGroups == null || string.IsNullOrEmpty(name)) return null;

            for (int i = 0; i < database.CharacterGroups.Count; i++)
            {
                CharacterGroup group = database.CharacterGroups[i];

                if (group != null && group.characterName == name) return group;
            }

            return null;
        }

        internal static CharacterSprite FindSprite(CharacterGroup group, string spriteName)
        {
            if (group?.sprites == null || string.IsNullOrEmpty(spriteName)) return null;

            for (int i = 0; i < group.sprites.Count; i++)
            {
                CharacterSprite sprite = group.sprites[i];

                if (sprite != null && sprite.spriteName == spriteName) return sprite;
            }

            return null;
        }

        #endregion LOOKUP
    }
}
#endif
