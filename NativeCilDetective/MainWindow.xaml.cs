using Mono.Cecil;
using NativeCilDetective.Disassembler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace NativeCilDetective
{
    public interface IAssemblyTreeViewChild
    {
        string TreeViewLabel { get; }
        IEnumerable<IAssemblyTreeViewChild> TreeViewChildren { get; }
        bool IsExpanded { get; set; }
        bool IsSelected { get; set; }
    }

    public partial class MainWindow
    {
        private readonly Brush StringBrush = new SolidColorBrush(Color.FromRgb(236, 118, 0));
        private readonly Brush MethodCallBrush = new SolidColorBrush(Color.FromRgb(167, 236, 33));
        private readonly Brush FieldBrush = new SolidColorBrush(Color.FromRgb(102, 217, 239));
        private readonly Brush OffsetBrush = new SolidColorBrush(Color.FromRgb(117, 113, 94));
        private readonly Brush ParsedInstructionBrush = new SolidColorBrush(Color.FromRgb(255, 216, 102));
        private readonly Brush UnknownInstructionBrush = new SolidColorBrush(Color.FromRgb(249, 250, 244));

        private NativeDetective detective;

        public MainWindow()
        {
            InitializeComponent();
            AnalysePath(@"C:\Users\Maxou\Desktop\Il2CppDumper\Il2CppDumper\bin\Debug\", @"C:\Users\Maxou\Downloads\PROClient x64\GameAssembly.dll");
        }

        abstract class TreeElementViewModelBase : IAssemblyTreeViewChild, INotifyPropertyChanged
        {
            public abstract string TreeViewLabel { get; }
            public abstract IEnumerable<IAssemblyTreeViewChild> TreeViewChildren { get; }

            public bool IsExpanded { get => isExpanded; set { isExpanded = value; NotifyPropertyChanged(); } }
            private bool isExpanded = false;

            public bool IsSelected { get => isSelected; set { isSelected = value; NotifyPropertyChanged(); } }
            private bool isSelected = false;

            public event PropertyChangedEventHandler PropertyChanged;

            protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        class MethodViewModel : TreeElementViewModelBase
        {
            public override string TreeViewLabel => $"{Method.Name}({string.Join(", ", Method.Parameters.Select(x => x.ParameterType.Name))}) : {Method.ReturnType.Name}";
            public override IEnumerable<IAssemblyTreeViewChild> TreeViewChildren => null;

            public MethodDefinition Method { get; private set; }

            public MethodViewModel(MethodDefinition method)
            {
                Method = method;
            }
        }

        class TypeViewModel : TreeElementViewModelBase
        {
            public override string TreeViewLabel => type.Name;
            public override IEnumerable<IAssemblyTreeViewChild> TreeViewChildren => children;

            private readonly TypeDefinition type;
            private readonly IList<IAssemblyTreeViewChild> children;

            public TypeViewModel(TypeDefinition type)
            {
                this.type = type;
                children = type.Methods.Select(x => new MethodViewModel(x)).OrderBy(x => x.TreeViewLabel).ToList<IAssemblyTreeViewChild>();
            }
        }

        class ModuleViewModel : TreeElementViewModelBase
        {
            public override string TreeViewLabel => module.Name;
            public override IEnumerable<IAssemblyTreeViewChild> TreeViewChildren => children;

            private readonly ModuleDefinition module;
            private readonly IList<IAssemblyTreeViewChild> children;

            public ModuleViewModel(ModuleDefinition module)
            {
                this.module = module;
                children = module.Types.Select(x => new TypeViewModel(x)).OrderBy(x => x.TreeViewLabel).ToList<IAssemblyTreeViewChild>();
            }
        }

        class AssemblyViewModel : TreeElementViewModelBase
        {
            public override string TreeViewLabel => assembly.Name.Name;
            public override IEnumerable<IAssemblyTreeViewChild> TreeViewChildren => children;

            private readonly AssemblyDefinition assembly;
            private readonly IList<IAssemblyTreeViewChild> children;

            public AssemblyViewModel(AssemblyDefinition assembly)
            {
                this.assembly = assembly;
                children = assembly.Modules.Select(x => new ModuleViewModel(x)).OrderBy(x => x.TreeViewLabel).ToList<IAssemblyTreeViewChild>();
            }
        }

        class RootViewModel
        {
            public IEnumerable<AssemblyViewModel> Assemblies { get; set; }
        }

        public void AnalysePath(string il2cppDumpPath, string nativeAssemblyPath)
        {
            detective = new NativeDetective(il2cppDumpPath, nativeAssemblyPath);

            AssemblyTreeView.DataContext = new RootViewModel
            {
                Assemblies = detective.Offsets.Assemblies.Select(x => new AssemblyViewModel(x))
            };

            SetCodeContent("Click somewhere!");
        }

        private void SetCodeContent(string code)
        {
            var paragraph = new Paragraph(new Run(code));

            CodeRichTextBox.Document.Blocks.Clear();
            CodeRichTextBox.Document.Blocks.Add(paragraph);
            CodeRichTextBox.ScrollToHome();
        }

        private void SetCodeContent(IList<TranslatedInstruction> instructions)
        {
            CodeRichTextBox.Document.Blocks.Clear();
            CodeRichTextBox.ScrollToHome();

            var paragraph = new Paragraph();
            AddInstructionsToTextBox(instructions, paragraph, 0, instructions.Count);
            CodeRichTextBox.Document.Blocks.Add(paragraph);
        }

        private void AddInstructionsToTextBox(IList<TranslatedInstruction> instructions, Paragraph paragraph, int begin, int count)
        {
            var inlines = new List<Inline>();
            foreach (var instruction in instructions.Skip(begin).Take(count))
            {
                inlines.Add(new Run(instruction.InstructionOffset.ToString("X8") + ": ")
                {
                    Foreground = OffsetBrush
                });
                if (instruction.StringContent != null)
                {
                    inlines.Add(new Run(instruction.AsmInstruction.ToString())
                    {
                        Foreground = ParsedInstructionBrush
                    });
                    inlines.Add(new Run(" // \"" + instruction.StringContent + "\"")
                    {
                        Foreground = StringBrush
                    });
                }
                else if (instruction.CalledMethod != null)
                {
                    inlines.Add(new Run(instruction.AsmInstruction.ToString())
                    {
                        Foreground = ParsedInstructionBrush
                    });
                    inlines.Add(new Run(" // " + instruction.CalledMethod.FullName)
                    {
                        Foreground = MethodCallBrush
                    });
                }
                else if (instruction.UsedField != null)
                {
                    inlines.Add(new Run(instruction.AsmInstruction.ToString())
                    {
                        Foreground = ParsedInstructionBrush
                    });
                    inlines.Add(new Run(" // " + instruction.UsedField.FullName)
                    {
                        Foreground = FieldBrush
                    });
                }
                else
                {
                    inlines.Add(new Run(instruction.AsmInstruction.ToString())
                    {
                        Foreground = UnknownInstructionBrush
                    });
                }
                inlines.Add(new LineBreak());
            }

            paragraph.Inlines.AddRange(inlines);
        }

        private void AssemblyTreeView_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem senderItem)
            {
                senderItem.BringIntoView();
            }

            MethodMenuItem.Visibility = AssemblyTreeView.SelectedItem is MethodViewModel ? Visibility.Visible : Visibility.Hidden;
            
            if (AssemblyTreeView.SelectedItem is MethodViewModel methodViewModel)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var instructions = detective.DisassembleMethod(methodViewModel.Method);
                sw.Stop();
                Console.WriteLine($"Took {sw.ElapsedMilliseconds} ms to disassemble.");

                if (instructions != null)
                {
                    SetCodeContent(instructions);
                }
                else
                {
                    SetCodeContent(methodViewModel.TreeViewLabel);
                }
            }
            else if (AssemblyTreeView.SelectedItem is IAssemblyTreeViewChild treeViewItem)
            {
                SetCodeContent(treeViewItem.TreeViewLabel);
            }
            else
            {
                SetCodeContent("");
            }
        }

        private void OpenAndSelectMethod(MethodDefinition method)
        {
            foreach (var item in AssemblyTreeView.Items)
            {
                var treeViewItem = (IAssemblyTreeViewChild)item;
                if (OpenAndSelectMethod(treeViewItem, method))
                {
                    treeViewItem.IsExpanded = true;
                }
            }
        }

        private bool OpenAndSelectMethod(IAssemblyTreeViewChild parent, MethodDefinition method)
        {
            foreach (var item in parent.TreeViewChildren)
            {
                if (item is MethodViewModel methodViewModel)
                {
                    if (methodViewModel.Method == method)
                    {
                        methodViewModel.IsSelected = true;
                        return true;
                    }
                }
                else if (item is IAssemblyTreeViewChild child)
                {
                    if (OpenAndSelectMethod(child, method))
                    {
                        child.IsExpanded = true;
                        return true;
                    }
                }
            }
            return false;
        }

        private class UsageViewModel
        {
            public string Label { get => $"{usage.Method.FullName} (x{usage.Count})"; }
            public MethodDefinition Method { get => usage.Method; }

            private readonly MethodUsageFinder.Usage usage;

            public UsageViewModel(MethodUsageFinder.Usage usage)
            {
                this.usage = usage;
            }
        }

        private void MethodFindUsages_Click(object sender, RoutedEventArgs e)
        {
            if (AssemblyTreeView.SelectedItem is MethodViewModel methodViewModel)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var usages = detective.UsageFinder.FindUsages(methodViewModel.Method);
                sw.Stop();
                Console.WriteLine($"Took {sw.ElapsedMilliseconds} ms to find usages.");

                UsageResultsListView.Items.Clear();
                foreach (var usage in usages)
                {
                    UsageResultsListView.Items.Add(new UsageViewModel(usage));
                }
            }
        }

        private void UsageResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem listViewItem)
            {
                if (listViewItem.Content is UsageViewModel usageViewModel)
                {
                    OpenAndSelectMethod(usageViewModel.Method);
                }
            }
        }
    }
}
