"""One-shot Windows display test using an external turing-smart-screen-python checkout."""

import argparse
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import time


def diagnostic_frame(update=False):
    from PIL import Image, ImageDraw, ImageFont

    frame = Image.new("RGB", (1920, 480), "#101a32")
    draw = ImageDraw.Draw(frame)
    font_path = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/segoeui.ttf"

    def label(x, y, text, size=24, color="#ffffff"):
        draw.text((x, y), text, font=ImageFont.truetype(str(font_path), size), fill=color)

    draw.rectangle((0, 0, 1919, 479), outline="#40cde3", width=3)
    label(16, 12, "ALTO / SINISTRA", 20, "#40cde3")
    label(1640, 12, "ALTO / DESTRA", 20, "#40cde3")
    label(350, 88, "DISCORD OVERLAY", 64)
    label(354, 175, "TEST DISPLAY 1920 x 480", 30, "#40cde3")
    label(354, 234, "Schermata di prova: nessun dato Discord o sensore reale", 25)
    label(354, 285, "Riquadro previsto sotto le barre CPU / GPU / RAM", 25)
    label(354, 395, "Per tornare a Earth Theme: riaprire TURZX e avviare il tema", 24)
    for y, name, width in [(215, "CPU", 70), (241, "GPU", 40), (267, "RAM", 100)]:
        label(15, y - 9, name, 17)
        draw.rectangle((105, y, 245, y + 9), fill="#ffffff")
        draw.rectangle((105, y, 105 + width, y + 9), fill="#40cde3")
    for x, color, name in [(1410, "#ff0000", "ROSSO"), (1570, "#00ff00", "VERDE"), (1730, "#0000ff", "BLU")]:
        draw.rectangle((x, 90, x + 120, 155), fill=color)
        label(x, 165, name, 18)
    draw.rectangle((15, 301, 262, 463), fill="#101a32", outline="#40cde3")
    label(25, 308, "DISCORD / DEMO", 20, "#40cde3")
    label(25, 341, "Utente: microfono ON", 18)
    label(25, 371, "Amico: MUTE", 18)
    label(25, 407, "UPDATE 02" if update else "FRAME 01", 24, "#40cde3")
    return frame


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--driver-root", type=Path, help="External turing-smart-screen-python checkout")
    parser.add_argument("--port", default="COM5")
    parser.add_argument("--output", type=Path, default=Path(tempfile.gettempdir()) / "discord-overlay-turzx-probe/results")
    parser.add_argument("--send", action="store_true", help="Stop the current video and send the test to the display")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    first = diagnostic_frame()
    second = diagnostic_frame(update=True)
    first.save(output / "probe-frame-01.png")
    second.save(output / "probe-frame-02.png")
    print(f"Diagnostic images: {output}", flush=True)
    if not args.send:
        return
    if not args.driver_root or not (args.driver_root / "library/lcd/lcd_comm_rev_c.py").is_file():
        parser.error("--send requires --driver-root with the external driver checkout")

    sys.path.insert(0, str(args.driver_root.resolve()))
    # The external library writes log.log relative to the working directory.
    os.chdir(output)
    import serial
    from serial.tools.list_ports import comports
    from library.lcd.lcd_comm import Orientation
    from library.lcd.lcd_comm_rev_c import Command, LcdCommRevC, SubRevision

    matches = [p for p in comports() if p.device.lower() == args.port.lower()]
    if len(matches) != 1 or (matches[0].vid, matches[0].pid) != (0x0525, 0xA4A7):
        raise RuntimeError(f"{args.port} is not the expected 0525:A4A7 display")

    class ProbeDisplay(LcdCommRevC):
        def openSerial(self):
            # Fail immediately if another application owns the port; no retries or resets.
            self.lcd_serial = serial.Serial(self.com_port, 115200, timeout=2, write_timeout=10, rtscts=True)

        def WriteLine(self, data):
            # The upstream convenience method suppresses write timeouts. A probe must fail.
            if self.lcd_serial.write(data) != len(data):
                raise RuntimeError("Incomplete serial write")

        def ReadData(self, size):
            response = self.lcd_serial.read(size)
            if not response:
                raise RuntimeError("No status response from the display")
            report["status_responses"].append({
                "length": len(response),
                "message": response.strip(b"\0").decode("ascii", errors="replace"),
                "prefix_hex": response[:32].hex(),
            })
            return response

    report = {"port": args.port, "status_responses": [], "full_frame_sent": False, "partial_frame_sent": False}
    lcd = None
    try:
        lcd = ProbeDisplay(com_port=args.port, display_width=480, display_height=1920)
        lcd.serial_flush_input()
        lcd._send_command(Command.HELLO)
        response = lcd.serial_read(23).decode("ascii", errors="replace").strip("\0\r\n ")
        report["device_response"] = response
        print(f"Device response: {response}", flush=True)
        # The observed ID is chs_88inch.dev1_rom1.90; do not silently guess ROM 87.
        match = re.fullmatch(r"chs_88inch\.dev\d+_rom\d+\.(\d+)", response)
        if not match or not 80 <= int(match[1]) <= 100:
            raise RuntimeError(f"Unrecognized device/ROM: {response!r}")
        lcd.sub_revision = SubRevision.REV_8INCH
        lcd.rom_version = int(match[1])
        # Only configure the host encoder; do not send persistent display options.
        lcd.orientation = Orientation.LANDSCAPE
        lcd.ScreenOn()
        lcd.DisplayPILImage(first)
        report["full_frame_sent"] = True
        print("Full test frame sent; testing a partial update in two seconds.", flush=True)
        time.sleep(2)
        lcd.DisplayPILImage(second.crop((15, 301, 263, 464)), x=15, y=301)
        report["partial_frame_sent"] = True
        print("Partial update sent. Verify UPDATE 02 at bottom left on the physical display.", flush=True)
    except Exception as exc:
        report["error"] = str(exc)
        raise
    finally:
        if lcd is not None:
            lcd.closeSerial()
        (output / "probe-result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
