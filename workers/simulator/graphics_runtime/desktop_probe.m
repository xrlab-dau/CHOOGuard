#import <Foundation/Foundation.h>
#import <Metal/Metal.h>
#import <ApplicationServices/ApplicationServices.h>
#import <Security/AuthSession.h>
#include <unistd.h>
#include <pwd.h>
#include <errno.h>
#include <sys/types.h>

int main(void) {
  @autoreleasepool {
    NSMutableDictionary *r = [NSMutableDictionary dictionary];
    r[@"schema"] = @"chooguard.graphics-desktop-probe.v1";
    r[@"uid"] = @(getuid()); r[@"euid"] = @(geteuid()); r[@"pid"] = @(getpid());
    errno=0; int count=getgroups(0,NULL); r[@"getgroups_count"] = @(count); r[@"getgroups_count_errno"]=@(errno);
    gid_t groups[64]; errno=0; int actual=getgroups(64,groups); r[@"getgroups_fixed_count"] = @(actual); r[@"getgroups_fixed_errno"]=@(errno);
    errno=0; struct passwd *pw=getpwuid(getuid()); r[@"getpwuid_found"]=@(pw!=NULL); r[@"getpwuid_errno"]=@(errno);
    SecuritySessionId sid=0; SessionAttributeBits attrs=0;
    OSStatus status=SessionGetInfo(callerSecuritySession,&sid,&attrs);
    r[@"security_session_status"]=@(status); r[@"security_session_id"]=@(sid); r[@"security_session_attributes"]=@(attrs);
    r[@"security_session_has_graphic_access"]=@((attrs&sessionHasGraphicAccess)!=0);
    CFDictionaryRef session=CGSessionCopyCurrentDictionary();
    r[@"cg_session_available"]=@(session!=NULL);
    if(session){r[@"cg_session"]=(__bridge NSDictionary*)session;CFRelease(session);}
    uint32_t displays=0; CGError err=CGGetOnlineDisplayList(0,NULL,&displays);
    r[@"online_display_error"]=@(err); r[@"online_display_count"]=@(displays);
    id<MTLDevice> device=MTLCreateSystemDefaultDevice();
    r[@"metal_device_available"]=@(device!=nil);
    if(device){
      r[@"metal_device_name"]=device.name;
      MTLTextureDescriptor *desc=[MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatRGBA8Unorm width:4 height:4 mipmapped:NO];
      desc.storageMode=MTLStorageModeShared; desc.usage=MTLTextureUsageRenderTarget;
      id<MTLTexture> texture=[device newTextureWithDescriptor:desc];
      id<MTLCommandQueue> queue=[device newCommandQueue]; id<MTLCommandBuffer> command=[queue commandBuffer];
      MTLRenderPassDescriptor *pass=[MTLRenderPassDescriptor renderPassDescriptor];
      pass.colorAttachments[0].texture=texture; pass.colorAttachments[0].loadAction=MTLLoadActionClear;
      pass.colorAttachments[0].storeAction=MTLStoreActionStore; pass.colorAttachments[0].clearColor=MTLClearColorMake(0.25,0.5,0.75,1);
      id<MTLRenderCommandEncoder> encoder=[command renderCommandEncoderWithDescriptor:pass];
      [encoder endEncoding]; [command commit]; [command waitUntilCompleted];
      r[@"metal_command_status"]=@(command.status);
      if(command.error)r[@"metal_command_error"]=command.error.localizedDescription;
      uint8_t bytes[64]={0}; [texture getBytes:bytes bytesPerRow:16 fromRegion:MTLRegionMake2D(0,0,4,4) mipmapLevel:0];
      r[@"metal_render_pixel"] = @[@(bytes[0]),@(bytes[1]),@(bytes[2]),@(bytes[3])];
      r[@"metal_render_verified"] = @(command.status==MTLCommandBufferStatusCompleted && bytes[0]>=63 && bytes[0]<=64 && bytes[1]==128 && bytes[2]>=191 && bytes[2]<=192 && bytes[3]==255);
    }
    NSData *json=[NSJSONSerialization dataWithJSONObject:r options:NSJSONWritingPrettyPrinted error:nil];
    fwrite(json.bytes,1,json.length,stdout); fputc('\n',stdout);
  }
  return 0;
}
