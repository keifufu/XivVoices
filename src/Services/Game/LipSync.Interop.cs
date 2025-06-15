namespace XivVoices.Services;

public partial class LipSync
{
  public enum CharacterMode : byte
  {
    None = 0,
    EmoteLoop = 3,
  }

  [StructLayout(LayoutKind.Explicit)]
  public unsafe struct ActorMemory
  {
    [FieldOffset(0x09B0)] public AnimationMemory Animation;
    [FieldOffset(0x22CC)] public byte CharacterMode;
  }

  [StructLayout(LayoutKind.Explicit)]
  public unsafe struct AnimationMemory
  {
    [FieldOffset(0x2D8)] public ushort LipsOverride;
  }

  private unsafe void TrySetLipsOverride(IntPtr character, ushort newLipsOverride)
  {
    if (character == IntPtr.Zero) return;
    ActorMemory* actorMemory = (ActorMemory*)character;
    if (actorMemory == null) return;
    AnimationMemory* animationMemory = (AnimationMemory*)Unsafe.AsPointer(ref actorMemory->Animation);
    if (animationMemory == null) return;
    animationMemory->LipsOverride = newLipsOverride;
  }

  private unsafe CharacterMode TryGetCharacterMode(IntPtr character)
  {
    if (character == IntPtr.Zero) return CharacterMode.None;
    ActorMemory* actorMemory = (ActorMemory*)character;
    if (actorMemory == null) return CharacterMode.None;
    return (CharacterMode)actorMemory->CharacterMode;
  }

  private unsafe void TrySetCharacterMode(IntPtr character, CharacterMode characterMode)
  {
    if (character == IntPtr.Zero) return;
    ActorMemory* actorMemory = (ActorMemory*)character;
    if (actorMemory == null) return;
    actorMemory->CharacterMode = (byte)characterMode;
  }
}
