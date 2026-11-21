"""F26-R03 structural WPF safety tests: static XAML review, never launch fm.exe."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
NS="{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
class WpfStructuralTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.main=ET.parse(ROOT/"src/FMRTE2008.Desktop/MainWindow.xaml").getroot()
        cls.dialog=ET.parse(ROOT/"src/FMRTE2008.Desktop/OfflineProfilesWindow.xaml").getroot()
        cls.code=(ROOT/"src/FMRTE2008.Desktop/OfflineProfilesWindow.xaml.cs").read_text(encoding="utf-8")
    def test_main_connection_remains_disabled(self):
        buttons=[x for x in self.main.iter(NS+"Button") if "Conectar ao FM2008" in x.get("Content","")]
        self.assertEqual(len(buttons),1)
        self.assertEqual(buttons[0].get("IsEnabled"),"False")
    def test_demo_entry_point(self):
        buttons=[x for x in self.main.iter(NS+"Button") if x.get("Click")=="OpenOfflineProfiles_Click"]
        self.assertEqual(len(buttons),1)
        self.assertIn("offline",buttons[0].get("Content","").lower())
    def test_both_data_grids_read_only(self):
        grids=list(self.dialog.iter(NS+"DataGrid"))
        self.assertEqual(len(grids),2)
        self.assertTrue(all(g.get("IsReadOnly")=="True" for g in grids))
        self.assertTrue(all(g.get("CanUserAddRows")=="False" for g in grids))
    def test_explicit_fiction_disclosure(self):
        blocks=[x.get("Text","") for x in self.dialog.iter(NS+"TextBlock")]
        self.assertTrue(any("DADOS INVENTADOS" in s and "SEM PROCESSO" in s for s in blocks))
    def test_no_native_process_api_in_dialog(self):
        for word in ("ReadProcessMemory","WriteProcessMemory","OpenProcess(","Process.GetProcess","DllImport","LoadLibrary"):
            self.assertNotIn(word,self.code)
    def test_presenter_selection_and_empty_result(self):
        self.assertIn("_presenter.Select(",self.code)
        self.assertIn("_presenter.Search(",self.code)
        self.assertIn("ShowDetails(null)",self.code)
        self.assertIn("AttributesGrid.ItemsSource = player?.Attributes;",self.code)
    def test_no_save_edit_actions(self):
        actions=[x.get("Content","").lower() for x in self.dialog.iter(NS+"Button")]
        self.assertFalse(any("save" in a or "salvar" in a or "editar" in a for a in actions))
if __name__=="__main__":unittest.main(verbosity=2)
