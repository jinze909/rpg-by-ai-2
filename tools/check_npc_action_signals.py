#!/usr/bin/env python3
"""Validate the committed 4+4 distinct NPC/escort micro-animations.
NPC and rescue identities remain their accepted source whole-body Sprites.
"""
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
source=ROOT/"Assets/Resources/Frontier/Art/NpcActionSignals.png"
meta=Path(str(source)+".meta")
actor=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs"
rescue=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierExpansionUI.cs"
runtime=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierNpcActionSignals.cs"
runner=ROOT/"tests/RuntimeCombatInputChecks.cs"

def inspect(art):
    assert art.size==(96,40),("NPC_SIGNAL_DIMENSIONS",art.size)
    all_frames=[]
    for row in range(2):
        family=[]
        for frame in range(4):
            cells=list(art.crop((frame*24,row*20,(frame+1)*24,(row+1)*20)).getdata())
            opaque=[(i%24,i//24,px) for i,px in enumerate(cells) if px[3]>0]
            assert 35<len(opaque)<220,("PAINTED_SIGNAL_CELL_MISSING",row,frame,len(opaque))
            assert len({px for _,_,px in opaque})>=5,("SIGNAL_FLAT_COLOR",row,frame)
            assert min(x for x,_,_ in opaque)>=1 and max(x for x,_,_ in opaque)<=22,("SIGNAL_CROPPED_X",row,frame)
            assert max(y for _,y,_ in opaque)<=19,("SIGNAL_CROPPED_Y",row,frame)
            assert any(px[3]>=245 for _,_,px in opaque),("SIGNAL_NO_OPAQUE_CORE",row,frame)
            family.append(tuple(cells))
        assert len(set(family))==4,("SIGNAL_FRAMES_DUPLICATED",row)
        all_frames+=family
    assert len(set(all_frames))==8,"TALK_COPIED_AS_ALERT"

def main():
    assert source.exists() and meta.exists(),"NPC_ART_AND_GUID_REQUIRED"
    with Image.open(source) as file:
        assert file.format=="PNG" and file.mode=="RGBA"
        artwork=file.copy()
    inspect(artwork)
    copied=Image.new("RGBA",(96,40))
    for row in range(2):
        block=artwork.crop((0,row*20,24,(row+1)*20))
        for i in range(4):copied.paste(block,(i*24,row*20))
    try:inspect(copied)
    except AssertionError:pass
    else:raise AssertionError("SIGNAL_MUTATION_DETECTOR_INACTIVE")
    m=meta.read_text()
    for string in ("guid: 22f40889e6d14878aeb209c8531c5b17",
        "filterMode: 0","enableMipMap: 0","alphaIsTransparency: 1",
        "textureCompression: 0"):
        assert string in m,("NPC_SIGNAL_IMPORTER",string)
    npc=actor.read_text()
    rescue_src=rescue.read_text()
    render=runtime.read_text()
    for item in ('if(isTalking&&!drawingOutline)',
        "DrawNpcActionSignal(p,0,npcKey);"):
        assert item in npc,("PINNED_SPEAKER_WIRING",item)
    for item in ("Game.ReliefEscortUnderAttack||justHit",
        "DrawNpcActionSignal(p,1,14);"):
        assert item in rescue_src,("RESCUE_SIGNAL_NOT_IN_PRODUCTION",item)
    for item in ('"Frontier/Art/NpcActionSignals"',
        "imported.width==96&&","imported.height==40",
        "Atlas(npcActionSignalSheet,4,2,row*4+frame,",
        "Game.save.reducedMotion"):
        assert item in render,("ANIMATED_SIGNAL_NOT_IN_RENDERER",item)
    assert "ActorRegistrationChecks.VerifyNpcActionSignalProduction," in runner.read_text(), (
        "PRODUCTION_NPC_QA_NOT_EXECUTED_BY_ACTUAL_CI")
    folder=ROOT/".starfall-checks/sprite-qa"
    folder.mkdir(parents=True,exist_ok=True)
    artwork.resize((96*6,40*6),Image.Resampling.NEAREST).save(
        folder/"npc-pinned-dialogue-and-escort-eight-poses.png")
    print("PASS eight unique hand-painted pixel NPC/survivor cues, Unity importer GUID, actual world hooks, adversarial frame mutation; NATIVE_UNVERIFIED")

if __name__=="__main__":
    main()
