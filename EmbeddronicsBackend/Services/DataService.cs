using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Services
{
    public interface IDataService<T>
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task<T> CreateAsync(T entity);
        Task<T?> UpdateAsync(int id, T entity);
        Task<bool> DeleteAsync(int id);
    }

    public class InMemoryDataService<T> : IDataService<T> where T : class
    {
        protected Dictionary<int, T> _data = new();
        protected int _nextId = 1;

        public virtual Task<IEnumerable<T>> GetAllAsync()
        {
            Serilog.Log.Information("Getting all {Type} records", typeof(T).Name);
            return Task.FromResult<IEnumerable<T>>(_data.Values);
        }

        public virtual Task<T?> GetByIdAsync(int id)
        {
            Serilog.Log.Information("Getting {Type} by Id: {Id}", typeof(T).Name, id);
            _data.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }

        public virtual Task<T> CreateAsync(T entity)
        {
            var prop = entity.GetType().GetProperty("Id");
            if (prop != null)
            {
                prop.SetValue(entity, _nextId);
            }
            _data[_nextId] = entity;
            Serilog.Log.Information("Created new {Type} record with Id: {Id}", typeof(T).Name, _nextId);
            _nextId++;
            return Task.FromResult(entity);
        }

        public virtual Task<T?> UpdateAsync(int id, T entity)
        {
            var prop = entity.GetType().GetProperty("Id");
            if (prop != null) prop.SetValue(entity, id);

            if (_data.ContainsKey(id))
            {
                _data[id] = entity;
                Serilog.Log.Information("Updated {Type} with Id: {Id}", typeof(T).Name, id);
                return Task.FromResult<T?>(entity);
            }
            Serilog.Log.Warning("Failed to update {Type} with Id: {Id} - not found", typeof(T).Name, id);
            return Task.FromResult<T?>(null);
        }

        public virtual Task<bool> DeleteAsync(int id)
        {
            if (_data.Remove(id))
            {
                Serilog.Log.Information("Deleted {Type} with Id: {Id}", typeof(T).Name, id);
                return Task.FromResult(true);
            }
            Serilog.Log.Warning("Failed to delete {Type} with Id: {Id} - not found", typeof(T).Name, id);
            return Task.FromResult(false);
        }
    }

    public class JsonDataService<T> : InMemoryDataService<T> where T : class
    {
        protected readonly string _filePath;
        private readonly string _fileName;

        public JsonDataService(string fileName)
        {
            _fileName = fileName;
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", fileName);
            Reload();
        }

        public void Reload()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<T>>(json, new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    if (items != null)
                    {
                        _data = items.ToDictionary(x => (int)x.GetType().GetProperty("Id")?.GetValue(x)!);
                        
                        if (_data.Any())
                        {
                            _nextId = _data.Keys.Max() + 1;
                        }
                        else
                        {
                            _nextId = 1;
                        }
                        Serilog.Log.Information("Loaded {Count} {Type} records from {File} into dictionary", _data.Count, typeof(T).Name, _fileName);
                    }
                }
                else
                {
                    Serilog.Log.Warning("Data file not found: {File}. Starting with empty dictionary.", _filePath);
                    _data = new Dictionary<int, T>();
                    _nextId = 1;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error loading data from {File}", _fileName);
                _data = new Dictionary<int, T>();
                _nextId = 1;
            }
        }

        public override async Task<T> CreateAsync(T entity)
        {
            var result = await base.CreateAsync(entity);
            SaveToFile();
            return result;
        }

        public override async Task<T?> UpdateAsync(int id, T entity)
        {
            var result = await base.UpdateAsync(id, entity);
            if (result != null) SaveToFile();
            return result;
        }

        public override async Task<bool> DeleteAsync(int id)
        {
            var result = await base.DeleteAsync(id);
            if (result) SaveToFile();
            return result;
        }

        protected void SaveToFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(_data.Values.ToList(), new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_filePath, json);
                Serilog.Log.Information("Saved {Type} data to {File}", typeof(T).Name, _fileName);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error saving data to {File}", _fileName);
            }
        }
    }
}
