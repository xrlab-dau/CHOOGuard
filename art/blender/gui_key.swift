import Cocoa
let pid=pid_t(CommandLine.arguments[1])!,key=CGKeyCode(CommandLine.arguments[2])!
for down in [true,false] { let event=CGEvent(keyboardEventSource:nil,virtualKey:key,keyDown:down)!;if CommandLine.arguments.count>3{event.flags = .maskShift};event.postToPid(pid);usleep(120000) }
