import Cocoa
let x=Double(CommandLine.arguments[1])!, y=Double(CommandLine.arguments[2])!
let point=CGPoint(x:x,y:y)
CGEvent(mouseEventSource:nil,mouseType:.mouseMoved,mouseCursorPosition:point,mouseButton:.left)?.post(tap:.cghidEventTap)
usleep(200000)
if CommandLine.arguments.count>3 {
CGEvent(mouseEventSource:nil,mouseType:.leftMouseDown,mouseCursorPosition:point,mouseButton:.left)?.post(tap:.cghidEventTap)
usleep(150000)
CGEvent(mouseEventSource:nil,mouseType:.leftMouseUp,mouseCursorPosition:point,mouseButton:.left)?.post(tap:.cghidEventTap)
}
