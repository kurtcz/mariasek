using System.Text;

public interface IStringLogger
{
    void Append(string s);
    void AppendLine(string s);
    void AppendFormat(string format, params object[] args);
    void Clear();
}

public class StringLogger : IStringLogger
{
    private StringBuilder _sb;
    private bool _doLog;

    public StringLogger(bool doLog = true)
    {
        _doLog = doLog;
        if (_doLog)
        {
            _sb = new StringBuilder();
        }
    }

    public void Append(string s)
    {
        if (_doLog)
        {
            try
            {
                _sb.Append(s);
            }
            catch
            {
                _doLog = false;
            }
        }
    }

    public void AppendLine(string s)
    {
        if (_doLog)
        {
            try
            {
                _sb.AppendLine(s);
            }
            catch
            {
                _doLog = false;
            }
        }
    }

    public void AppendFormat(string format, params object[] args)
    {
        if (_doLog)
        {
            try
            {
                _sb.AppendFormat(format, args);
            }
            catch
            {
                _doLog = false;
            }
        }
    }

    public void Clear()
    {
        if (_doLog)
        {
            try
            {
                _sb.Clear();
            }
            catch
            {
                _doLog = false;
            }
        }
    }

    public override string ToString()
    {
        return _doLog ? _sb?.ToString() ?? string.Empty : string.Empty;
    }
}