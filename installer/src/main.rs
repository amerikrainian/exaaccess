// Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
// https://github.com/rashadnaqeeb/NonVisualCalculus — by way of the Harkest
// Dungeon installer, https://github.com/amerikrainian/harkest-dungeon.

#![windows_subsystem = "windows"]

use exaaccess_installer::{cli, gui};

fn main() {
    if std::env::args().any(|a| a == "--cli") {
        attach_console();
        cli::run();
    } else {
        gui::run();
    }
}

fn attach_console() {
    #[cfg(target_os = "windows")]
    unsafe {
        windows_sys::Win32::System::Console::AttachConsole(
            windows_sys::Win32::System::Console::ATTACH_PARENT_PROCESS,
        );
    }
}
