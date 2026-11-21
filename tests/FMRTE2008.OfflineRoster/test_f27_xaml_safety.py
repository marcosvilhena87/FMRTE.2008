"""F27: structural checks only; no real FM process or GUI automation."""
from pathlib import Path
from xml.etree import ElementTree as ET
import unittest

ROOT=Path(__file__).resolve().parents[2]
W="{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
class TestOfflineRosterXaml(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.x=ET.parse(ROOT/"src/FMRTE2008.Desktop/OfflineRosterWindow.xaml").getroot()
        cls.main=ET.parse(ROOT/"src/FMRTE2008.Desktop/MainWindow.xaml").getroot()
        cls.code=(ROOT/"src/FMRTE2008.Desktop/OfflineRosterWindow.xaml.cs").read_text(encoding="utf-8")
    def test_all_grids_locked(self):
        grids=list(self.x.iter(W+"DataGrid"))
        self.assertEqual(len(grids),4)
        self.assertTrue(all(g.get("IsReadOnly")=="True" and g.get("CanUserAddRows")=="False" for g in grids))
    def test_offline_label(self):
        self.assertTrue(any("INTEIRAMENTE FICTÍCIOS" in t.get("Text","") and "SEM PROCESSO" in t.get("Text","") for t in self.x.iter(W+"TextBlock")))
    def test_three_groups(self):
        headers=[g.get("Header") for g in self.x.iter(W+"GroupBox")]
        self.assertEqual(headers,["Técnicos","Mentais","Físicos"])
    def test_f27_navigation_hooks(self):
        actions={b.get("Click") for b in self.x.iter(W+"Button")}
        self.assertEqual(actions,{"Search_Click","Next_Click","Previous_Click"})
        for fn in ("_presenter.Search(","_presenter.Select(","_presenter.Next(","_presenter.Previous("):
            self.assertIn(fn,self.code)
        self.assertIn("if (_refreshing) return;",self.code)
    def test_main_connection_remains_disabled(self):
        buttons=[b for b in self.main.iter(W+"Button") if "Conectar ao FM2008" in b.get("Content","")]
        self.assertEqual(len(buttons),1)
        self.assertEqual(buttons[0].get("IsEnabled"),"False")
    def test_entrypoint_exists(self):
        buttons=[b for b in self.main.iter(W+"Button") if b.get("Click")=="OpenOfflineRoster_Click"]
        self.assertEqual(len(buttons),1)
    def test_no_native_or_save_calls(self):
        for text in ("OpenProcess(","ReadProcessMemory","WriteProcessMemory","DllImport",
                     "Process.GetProcesses","File.WriteAllBytes","SaveGame","fm.exe"):
            self.assertNotIn(text,self.code)
if __name__=="__main__":unittest.main()
