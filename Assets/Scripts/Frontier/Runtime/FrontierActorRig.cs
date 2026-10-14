using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static float ActorCanvas(Texture2D sheet,int columns,float density)
        { return sheet==null?128*density:sheet.width/(float)columns*density; }
        private void ActorPart(Texture2D sheet,int columns,int rows,int frame,Rect part,Point foot,float density,Color color,bool flip)
        {
            if(sheet==null||part.width<=0||part.height<=0)return;
            float w=sheet.width/(float)columns,h=sheet.height/(float)rows;
            float localX=flip?w-part.x-part.width:part.x;
            Rect dest=new Rect(foot.x-w*density/2+localX*density,foot.y-h*density*120/128+part.y*density,part.width*density,part.height*density);
            float u=(frame%columns*w+part.x+(flip?part.width:0))/sheet.width;
            Rect uv=new Rect(u,1-(frame/columns*h+part.y+part.height)/sheet.height,part.width/sheet.width*(flip?-1:1),part.height/sheet.height);
            // Atlas() applies worldActorOpacity; five-piece HumanActor must
            // obey the identical opacity during occlusion and fades.
            Color previous=GUI.color;try{color.a*=worldActorOpacity;GUI.color=color;GUI.DrawTextureWithTexCoords(dest,sheet,uv);}finally{GUI.color=previous;}
        }
        private void HumanActor(Texture2D sheet,int columns,int rows,int frame,Point foot,float density,Color color,bool flip,Texture2D identity,int identityFrame,bool locomotion,bool flash=false,bool death=false,Texture2D unifiedAction=null)
        {
            Rect head=new Rect(),reference=new Rect();
            bool valid=sheet!=null&&identity!=null&&sheet.width==columns*256&&sheet.height==rows*256&&identity.width==4*256&&identity.height==3*256&&ActorFrameRegistration.TryHead(sheet,frame,out head)&&ActorFrameRegistration.TryHead(identity,identityFrame,out reference);
            if(death||!valid){Actor(flash&&sheet!=null?art.FlashMask(sheet):sheet,columns,rows,frame,foot,ActorCanvas(sheet,columns,density),color,flip);return;}
            Rect stable;if(ActorFrameRegistration.TryStableHead(identity,identityFrame/4,out stable))reference=stable;
            if(locomotion&&ActorFrameRegistration.TryStableHead(sheet,frame/4,out stable))head=stable;
            Texture2D bodySource=unifiedAction!=null?unifiedAction:sheet;
            Texture2D body=flash?art.FlashMask(bodySource):drawingOutline?art.OutlineMask(bodySource):bodySource;
            Texture2D face=flash?art.FlashMask(identity):drawingOutline?art.OutlineMask(identity):identity;
            if(drawingOutline)color=new Color(1,.56f,.35f,.9f);
            float w=sheet.width/(float)columns,h=sheet.height/(float)rows;
            // All pieces of a unified attack/walk frame share the SAME 256px
            // source origin and one 120/128 foot anchor. The head identity is
            // still the original canonical blonde hero from WalkVanguard.
            int bodyCols=unifiedAction!=null?1:columns;
            int bodyRows=unifiedAction!=null?1:rows;
            int bodyFrame=unifiedAction!=null?0:frame;
            ActorPart(body,bodyCols,bodyRows,bodyFrame,new Rect(0,0,w,head.y),foot,density,color,flip);
            ActorPart(body,bodyCols,bodyRows,bodyFrame,new Rect(0,head.y,head.x,head.height),foot,density,color,flip);
            ActorPart(body,bodyCols,bodyRows,bodyFrame,new Rect(head.x+head.width,head.y,w-head.x-head.width,head.height),foot,density,color,flip);
            ActorPart(body,bodyCols,bodyRows,bodyFrame,new Rect(0,head.y+head.height,w,h-head.y-head.height),foot,density,color,flip);
            float x=locomotion?128:head.x+head.width/2,y=locomotion?reference.y:head.y+head.height-reference.height;
            float dx=x-reference.x-reference.width/2,dy=y-reference.y;
            ActorPart(face,4,3,identityFrame,reference,foot+new Point((flip?-dx:dx)*density,dy*density),density,color,flip);
        }
    }
}
