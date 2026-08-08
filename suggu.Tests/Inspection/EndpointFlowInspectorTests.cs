using suggu.Core.Inspection;

namespace suggu.Tests.Inspection;

public sealed class EndpointFlowInspectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-flow-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Traces_controller_to_service_to_repository()
    {
        File.WriteAllText(Path.Combine(_root, "OrdersController.cs"), """
            public class OrdersController(IOrderService orderService)
            {
                public object Get(int id)
                {
                    return orderService.GetOrder(id);
                }
            }
            """);
        File.WriteAllText(Path.Combine(_root, "OrderService.cs"), """
            public interface IOrderService { object GetOrder(int id); }
            public class OrderService : IOrderService
            {
                private readonly IOrderRepository _repository;
                public OrderService(IOrderRepository repository) { _repository = repository; }
                public object GetOrder(int id) { return _repository.Load(id); }
            }
            """);
        File.WriteAllText(Path.Combine(_root, "OrderRepository.cs"), """
            public interface IOrderRepository { object Load(int id); }
            public class OrderRepository : IOrderRepository
            {
                public object Load(int id) { return new object(); }
            }
            """);

        var result = EndpointFlowInspector.Trace(_root, "Orders", "Get");

        Assert.True(result.Found, result.Error);
        var service = Assert.Single(result.Root!.Children);
        Assert.Equal("OrderService.GetOrder()", service.Description);
        Assert.Equal(FlowCertainty.Confirmed, service.Certainty);
        var repository = Assert.Single(service.Children);
        Assert.Equal("OrderRepository.Load()", repository.Description);
    }

    [Fact]
    public void Missing_controller_returns_clear_result()
    {
        var result = EndpointFlowInspector.Trace(_root, "Missing", "Get");
        Assert.False(result.Found);
        Assert.Contains("MissingController", result.Error);
    }

    [Fact]
    public void Resolves_common_mediatr_send_to_request_handler()
    {
        File.WriteAllText(Path.Combine(_root, "OrdersController.cs"), """
            public class OrdersController(ISender sender)
            {
                public async Task<object> Create()
                {
                    return await sender.Send(new CreateOrderCommand());
                }
            }
            """);
        File.WriteAllText(Path.Combine(_root, "CreateOrderHandler.cs"), """
            public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, object>
            {
                public Task<object> Handle(CreateOrderCommand request, CancellationToken token)
                {
                    return Task.FromResult<object>(new object());
                }
            }
            """);

        var result = EndpointFlowInspector.Trace(_root, "Orders", "Create");

        Assert.True(result.Found, result.Error);
        var handler = Assert.Single(result.Root!.Children);
        Assert.Equal("CreateOrderHandler.Handle(CreateOrderCommand)", handler.Description);
        Assert.Equal(FlowCertainty.Confirmed, handler.Certainty);
    }

    [Fact]
    public void Traces_concrete_mvc_dependencies_in_arbitrary_nested_folders()
    {
        var controllerFolder = Directory.CreateDirectory(Path.Combine(_root, "Presentation", "Web", "Endpoints"));
        var serviceFolder = Directory.CreateDirectory(Path.Combine(_root, "Anything", "BusinessLogic"));
        var dataFolder = Directory.CreateDirectory(Path.Combine(_root, "CustomStorage", "Queries"));
        File.WriteAllText(Path.Combine(controllerFolder.FullName, "CatalogController.cs"), """
            public class CatalogController(CatalogService catalogService)
            {
                public object Details(int id)
                {
                    return View(catalogService.Load(id));
                }

                private object View(object model) => model;
            }
            """);
        File.WriteAllText(Path.Combine(serviceFolder.FullName, "CatalogService.cs"), """
            public class CatalogService
            {
                private readonly CatalogStore store;
                public CatalogService(CatalogStore store) { this.store = store; }
                public object Load(int id) => store.Fetch(id);
            }
            """);
        File.WriteAllText(Path.Combine(dataFolder.FullName, "CatalogStore.cs"), """
            public class CatalogStore
            {
                public object Fetch(int id) => new object();
            }
            """);

        var result = EndpointFlowInspector.Trace(_root, "Catalog", "Details");

        Assert.True(result.Found, result.Error);
        var service = Assert.Single(result.Root!.Children);
        Assert.Equal("CatalogService.Load()", service.Description);
        var store = Assert.Single(service.Children);
        Assert.Equal("CatalogStore.Fetch()", store.Description);
    }

    [Fact]
    public void Finds_action_in_later_partial_controller_file()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "FeatureA"));
        var second = Directory.CreateDirectory(Path.Combine(_root, "FeatureB"));
        File.WriteAllText(Path.Combine(first.FullName, "ReportsController.Base.cs"), """
            public partial class ReportsController
            {
                private object Shared() => new object();
            }
            """);
        File.WriteAllText(Path.Combine(second.FullName, "ReportsController.Actions.cs"), """
            public partial class ReportsController
            {
                public object Summary() => new object();
            }
            """);

        var result = EndpointFlowInspector.Trace(_root, "Reports", "Summary");

        Assert.True(result.Found, result.Error);
        Assert.EndsWith("ReportsController.Actions.cs", result.Root!.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Shows_only_connected_source_files_and_resolves_query_rows_lambdas_and_aliases()
    {
        File.WriteAllText(Path.Combine(_root, "PostsController.cs"), """
            public class PostsController(ISender sender)
            {
                public async Task<object> GetAll()
                {
                    return await sender.Send(new GetPostsQuery());
                }
            }
            """);
        File.WriteAllText(Path.Combine(_root, "GetPostsHandler.cs"), """
            public class GetPostsHandler : IRequestHandler<GetPostsQuery, object>
            {
                private readonly IConnectionFactory _factory;
                public GetPostsHandler(IConnectionFactory factory) { _factory = factory; }
                public async Task<object> Handle(GetPostsQuery request, CancellationToken token)
                {
                    using var connection = _factory.Create();
                    var rows = await connection.QueryAsync<PostRow>();
                    return rows.Select(row => row.ToDto());
                }
            }
            """);
        File.WriteAllText(Path.Combine(_root, "ConnectionFactory.cs"), """
            public class ConnectionFactory : IConnectionFactory
            {
                public IDbConnection Create()
                {
                    var connection = new ExternalConnection();
                    connection.Open();
                    return connection;
                }
            }
            """);
        File.WriteAllText(Path.Combine(_root, "PostRow.cs"), """
            using PostEntity = Example.Domain.Post;
            public class PostRow
            {
                public object ToDto() => PostEntity.Calculate();
            }
            public class Post
            {
                public static object Calculate() => new object();
            }
            """);

        var result = EndpointFlowInspector.Trace(_root, "Posts", "GetAll");
        var steps = Flatten(result.Root!).ToList();

        Assert.Contains(steps, step => step.Description == "ConnectionFactory.Create()");
        Assert.Contains(steps, step => step.Description == "PostRow.ToDto()");
        Assert.Contains(steps, step => step.Description == "Post.Calculate()");
        Assert.DoesNotContain(steps, step => step.Description.Contains("connection.Open", StringComparison.Ordinal));
        Assert.DoesNotContain(steps, step => step.Description.Contains("rows.Select", StringComparison.Ordinal));
        Assert.DoesNotContain(steps, step => step.Certainty == FlowCertainty.Unresolved);
    }

    private static IEnumerable<EndpointFlowStep> Flatten(EndpointFlowStep step)
    {
        yield return step;
        foreach (var child in step.Children.SelectMany(Flatten)) yield return child;
    }
}
