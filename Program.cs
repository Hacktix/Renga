namespace Renga
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
                new Renga().Run();
            else
                new Renga(args).Run();
        }
    }
}