import Cocoa
import ApplicationServices
print("AX",AXIsProcessTrusted())
print("screen",NSScreen.main!.frame,"mouse",NSEvent.mouseLocation,"cg",CGEvent(source:nil)!.location)
print("displays",NSScreen.screens.map{$0.frame})
