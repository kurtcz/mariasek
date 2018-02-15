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
        if (doLog)
        {
            _sb = new StringBuilder();
        }
    }

    public void Append(string s)
    {
        if (_doLog)
        {
            _sb.Append(s);
        }
    }

    public void AppendLine(string s)
    {
        if (_doLog)
        {
            _sb.AppendLine(s);
        }
    }

    public void AppendFormat(string format, params object[] args)
    {
        if (_doLog)
        {
            _sb.AppendFormat(format, args);
        }
    }

    public void Clear()
    {
        if (_doLog)
        {
            _sb.Clear();
        }
    }

    public override string ToString()
    {
        return _doLog ? _sb.ToString() : string.Empty;
    }
}