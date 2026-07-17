using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using atp_enterprise_app_wpf.Models;
using atp_enterprise_app_wpf.Services;

namespace atp_enterprise_app_wpf.Views
{
    public partial class ProcedureManagerView : UserControl
    {
        private ObservableCollection<AtpProcedure> _procedures = new();
        private AtpProcedure? _selectedProcedure;

        public ProcedureManagerView()
        {
            InitializeComponent();
            LoadProcedures();
        }

        private void LoadProcedures()
        {
            _procedures = new ObservableCollection<AtpProcedure>(ProcedureLoaderService.Instance.ListProcedures());
            LstProcedures.ItemsSource = _procedures;
        }

        private void LstProcedures_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstProcedures.SelectedItem is AtpProcedure procedure)
            {
                _selectedProcedure = procedure;
                BindEditor(procedure);
            }
        }

        private void BindEditor(AtpProcedure procedure)
        {
            EditorPanel.IsEnabled = true;
            TxtTitle.Text = procedure.ProcedureTitle;
            TxtFamily.Text = procedure.ProductFamily;
            TxtRevision.Text = procedure.Revision;
            TxtAuthor.Text = procedure.Author;
            
            GridTestSequence.ItemsSource = procedure.TestSequence;
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var newProc = new AtpProcedure();
            ProcedureLoaderService.Instance.SaveProcedure(newProc);
            LoadProcedures();
            LstProcedures.SelectedItem = _procedures.FirstOrDefault(p => p.ProcedureId == newProc.ProcedureId);
        }

        private void BtnClone_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProcedure == null) return;
            var cloned = _selectedProcedure.Clone();
            ProcedureLoaderService.Instance.SaveProcedure(cloned);
            LoadProcedures();
            LstProcedures.SelectedItem = _procedures.FirstOrDefault(p => p.ProcedureId == cloned.ProcedureId);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProcedure == null) return;

            _selectedProcedure.ProcedureTitle = TxtTitle.Text;
            _selectedProcedure.ProductFamily = TxtFamily.Text;
            _selectedProcedure.Revision = TxtRevision.Text;
            _selectedProcedure.Author = TxtAuthor.Text;

            // GridTestSequence bindings update the TestSequence items directly

            ProcedureLoaderService.Instance.SaveProcedure(_selectedProcedure);
            LoadProcedures();
            MessageBox.Show("Procedure saved successfully.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
