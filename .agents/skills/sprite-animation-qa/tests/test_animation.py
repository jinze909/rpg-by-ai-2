import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from PIL import Image, ImageDraw

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "inspect_animation.py"


class OfflineAnimationQATests(unittest.TestCase):
    def test_frames_create_actual_visuals_and_warning(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            frames = []
            for i in range(3):
                im = Image.new("RGBA", (32, 32))
                draw = ImageDraw.Draw(im)
                if i != 2:
                    draw.rectangle((12 + i, 8, 20 + i, 30), fill=(80, 100, 250, 255))
                else:
                    draw.rectangle((0, 2, 8, 12), fill=(80, 100, 250, 255))
                p = root / f"f{i}.png"
                im.save(p)
                frames.append(str(p))
            out = root / "out"
            cmd = [sys.executable, str(SCRIPT), "--frames", *frames, "--fps", "8", "--scale", "3", "--out", str(out)]
            subprocess.run(cmd, check=True, capture_output=True, text=True)
            for file in ["filmstrip.png", "preview.gif", "onion_skin.png", "metrics.json", "report.md"]:
                self.assertTrue((out / file).exists(), file)
                self.assertGreater((out / file).stat().st_size, 30)
            data = json.loads((out / "metrics.json").read_text())
            self.assertEqual(data["frame_count"], 3)
            self.assertTrue(any("deviates" in s or "jump" in s for s in data["warnings"]))
            with Image.open(out / "preview.gif") as gif:
                self.assertEqual(gif.n_frames, 3)

    def test_atlas_indices(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            im = Image.new("RGBA", (64, 32), (0, 0, 0, 0))
            draw = ImageDraw.Draw(im)
            draw.rectangle((2, 2, 20, 25), fill=(200, 30, 20, 255))
            draw.rectangle((36, 2, 55, 25), fill=(20, 30, 200, 255))
            sheet = root / "atlas.png"
            im.save(sheet)
            out = root / "out"
            subprocess.run([sys.executable, str(SCRIPT), "--sheet", str(sheet), "--cols", "2", "--rows", "1", "--indices", "1,0", "--out", str(out)], check=True, capture_output=True, text=True)
            data = json.loads((out / "metrics.json").read_text())
            self.assertEqual([v["label"] for v in data["frames"]], ["cell 1", "cell 0"])


if __name__ == "__main__":
    unittest.main()
