// Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
// https://github.com/rashadnaqeeb/NonVisualCalculus — by way of the Harkest
// Dungeon installer, https://github.com/amerikrainian/harkest-dungeon.

fn main() {
    if std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default() == "windows" {
        // compile_for, not compile: the manifest requires elevation, which must
        // not apply to the test harness binary or `cargo test` cannot run.
        let _ = embed_resource::compile_for("app.rc", ["echopunks-installer"], embed_resource::NONE);
    }
}
