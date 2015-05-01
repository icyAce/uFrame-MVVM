using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Invert.Core;
using Invert.Core.GraphDesigner;
using Invert.uFrame.MVVM;
using uFrame.Graphs;

[TemplateClass(MemberGeneratorLocation.Both, ClassNameFormat = uFrameFormats.CONTROLLER_FORMAT)]
public partial class ControllerTemplate : Controller, IClassTemplate<ElementNode>, IClassRefactorable, IMethodRefactorable
{
    public TemplateContext<ElementNode> Ctx { get; set; }

    public string OutputPath
    {
        get { return Path2.Combine(Ctx.Data.Graph.Name, "Controllers"); }
    }

    public bool CanGenerate
    {
        get { return true; }
    }

    public void TemplateSetup()
    {

        Ctx.TryAddNamespace("UniRx");
        foreach (var property in Ctx.Data.PersistedItems.OfType<ITypedItem>())
        {
            var type = InvertApplication.FindTypeByName(property.RelatedTypeName);
            if (type == null) continue;

            Ctx.TryAddNamespace(type.Namespace);
        }

        if (Ctx.IsDesignerFile)
        {
            Ctx.CurrentDecleration.Attributes = MemberAttributes.Abstract;
        }

        Ctx.AddIterator("CommandMethod", _ => _.AllCommandHandlers.Where(p => string.IsNullOrEmpty(p.RelatedTypeName)));
        Ctx.AddIterator("CommandMethodWithArg", _ => _.AllCommandHandlers.Where(p => !string.IsNullOrEmpty(p.RelatedTypeName)));


        Ctx.AddIterator("OnCommandMethod",
            _ => _.LocalCommands);


        if (Ctx.Data.BaseNode == null)
        {
            Ctx.AddIterator("InstanceProperty",
                _ => _.GetParentNodes().OfType<SubsystemNode>().SelectMany(p => p.Instances).Distinct());
        }
        else
        {
            Ctx.AddCondition("InstanceProperty", _ => false);
        }
    }

    public string NameAsViewModel { get { return Ctx.Data.Name.AsViewModel(); } }


    [TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFill = AutoFillType.NameAndTypeWithBackingField, NameFormat = "{0}ViewModelManager")]
    public IViewModelManager ViewModelManager
    {
        get
        {
            Ctx.SetType(typeof(IViewModelManager)); // I force this so it doesn't change it
            Ctx.CurrentProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(InjectAttribute).ToCodeReference(), new CodeAttributeArgument(new CodePrimitiveExpression(Ctx.Data.Name))));
            return null;
        }
    }

    [TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFillType.NameAndTypeWithBackingField)]
    public ViewModel InstanceProperty
    {
        get
        {
            Ctx.CurrentProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                typeof(InjectAttribute).ToCodeReference(),
                new CodeAttributeArgument(new CodePrimitiveExpression(Ctx.ItemAs<InstancesReference>().Name))
                ));

            return null;
        }
        set
        {

        }
    }

    [TemplateMethod(MemberGeneratorLocation.Both)]
    public override void Setup()
    {
        base.Setup();
        Ctx._comment("This is called when the controller is created");
        if (Ctx.IsDesignerFile)
        {
//            foreach (var command in Ctx.Data.AllCommandHandlers)
//            {
//                Ctx._("this.OnEvent<{0}Command>().Subscribe(this.{0}Handler)", command.Name);
//            }
            //foreach (var command in Ctx.Data.Handlers.Where(p => !(p.SourceItem is CommandsChildItem)))
            //{
            //    Ctx._("this.OnEvent<{0}>().Subscribe(this.{0}Handler)", command.Name);
            //}

            //Ctx._("this.EventAggregator.OnViewModelCreated<{0}>().Subscribe(vm => this.Initialize{1}(vm as {0}));", Ctx.Data.Name.AsViewModel(), Ctx.Data.Name);
            //Ctx._("this.EventAggregator.OnViewModelDestroyed<{0}>().Subscribe(this.DisposingViewModel);", Ctx.Data.Name.AsViewModel());

        }


    }

    [TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFillType.NameAndType, NameFormat = "{0}ViewModels")]
    public IEnumerable<ViewModel> ViewModelItems
    {
        get
        {
            Ctx.SetTypeArgument(Ctx.Data.Name.AsViewModel());
            Ctx._("return {1}ViewModelManager.OfType<{0}>()", Ctx.Data.Name.AsViewModel(), Ctx.Data.Name);
            return null;
        }
    }

    [TemplateMethod(MemberGeneratorLocation.DesignerFile, CallBase = true)]
    public override void Initialize(ViewModel viewModel)
    {
        Ctx._comment("This is called when a viewmodel is created");
        if (!Ctx.IsDesignerFile) return;
        Ctx._("this.Initialize{0}((({1})(viewModel)))", Ctx.Data.Name, NameAsViewModel);
    }

    [TemplateMethod("Create{0}", MemberGeneratorLocation.DesignerFile, false)]
    public ViewModel CreateElement()
    {
        Ctx.SetType(NameAsViewModel);
        Ctx._("return (({0})(this.Create(Guid.NewGuid().ToString())))", NameAsViewModel);
        return null;
    }

    [TemplateMethod(MemberGeneratorLocation.DesignerFile, false)]
    public override ViewModel CreateEmpty()
    {
        Ctx._("return new {0}(this.EventAggregator)", NameAsViewModel);
        return null;
    }

    [TemplateMethod("Initialize{0}", MemberGeneratorLocation.Both, true)]
    public virtual void InitializeElement(ViewModel viewModel)
    {
        Ctx._comment("This is called when a {0} is created", NameAsViewModel);
        Ctx.CurrentMethod.Parameters[0].Type = new CodeTypeReference(NameAsViewModel);
        if (Ctx.IsDesignerFile)
        {
            foreach (var command in Ctx.Data.Commands)
            {
                Ctx._("viewModel.{0}.Action = this.{0}Handler", command.Name);
            }
            Ctx._("{0}ViewModelManager.Add(viewModel)", Ctx.Data.Name);
        }
            
    }

    [TemplateMethod(MemberGeneratorLocation.DesignerFile, true)]
    public override void DisposingViewModel(ViewModel viewModel)
    {
        base.DisposingViewModel(viewModel);
        Ctx._("{0}ViewModelManager.Remove(viewModel)", Ctx.Data.Name);
    }

    [TemplateMethod("{0}", MemberGeneratorLocation.Both, true)]
    public virtual void CommandMethod(ViewModel viewModel)
    {
        Ctx.CurrentMethod.Parameters[0].Type = new CodeTypeReference(Ctx.Item.Node.Name + "ViewModel");
        DoTransition();
    }

    [TemplateMethod("{0}", MemberGeneratorLocation.DesignerFile, true)]
    public virtual void OnCommandMethod(ViewModelCommand command)
    {

        Ctx.CurrentMethod.Name = Ctx.Item.Name + "Handler";
        if (Ctx.Item is CommandsChildItem)
        {
            Ctx.CurrentMethod.Parameters[0].Type = new CodeTypeReference(Ctx.Item.Name + "Command");
        }
        else
        {
            Ctx.CurrentMethod.Parameters[0].Type = new CodeTypeReference(Ctx.Item.Name);
        }

        if (Ctx.IsDesignerFile)
        {
            if (Ctx.Item is CommandsChildItem)
            {
                var c = Ctx.TypedItem;
                if (Ctx.ItemAs<CommandsChildItem>().OutputCommand != null)
                {
                    Ctx._("this.{0}(command.Sender as {1}, command)", c.Name, c.Node.Name.AsViewModel());
                }
                else if (string.IsNullOrEmpty(c.RelatedType))
                {
                    Ctx._("this.{0}(command.Sender as {1})", c.Name, c.Node.Name.AsViewModel());
                }
                else
                {
                    Ctx._("this.{0}(command.Sender as {1}, command.Argument)", c.Name, c.Node.Name.AsViewModel());
                }
            }

        }
    }
    private void DoTransition()
    {
        //if (Ctx.IsDesignerFile)
        //{
        //    var transition = Ctx.Item.OutputTo<TransitionsChildItem>();
        //    if (transition != null)
        //    {

        //        var stateMachineProperty =
        //            Ctx.Data.LocalProperties.FirstOrDefault(p => p.RelatedTypeNode is StateMachineNode);

        //        if (stateMachineProperty != null)
        //        {
        //            Ctx._("viewModel.{0}.Transition(\"{1}\")", stateMachineProperty.Name.AsSubscribableProperty(),
        //                transition.Name);
        //        }
        //    }
        //}
    }


    [TemplateMethod("{0}", MemberGeneratorLocation.Both, true)]
    public virtual void CommandMethodWithArg(ViewModel viewModel, object arg)
    {
        CommandMethod(viewModel);
        Ctx.CurrentMethod.Parameters[1].Type = new CodeTypeReference(Ctx.TypedItem.RelatedTypeName);
        DoTransition();

    }

    public IEnumerable<string> ClassNameFormats
    {
        get
        {

            yield return "{0}Controller";
            yield return "{0}ControllerBase";
        }
    }

    public IEnumerable<string> MethodFormats
    {
        get { yield return "Initialize{0}"; }
    }
}
