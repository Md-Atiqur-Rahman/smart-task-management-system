import { SseParser } from './sse-parser';

describe('SseParser (text/event-stream)', () => {
  it('parses one event with a type and JSON data', () => {
    const events = new SseParser().push('event: notification\ndata: {"id":"n1"}\n\n');

    expect(events).toEqual([{ event: 'notification', data: '{"id":"n1"}' }]);
  });

  it('joins an event split across two network chunks (Review Focus 5)', () => {
    const parser = new SseParser();

    expect(parser.push('event: notifi')).toEqual([]);
    expect(parser.push('cation\ndata: {"id":')).toEqual([]);
    expect(parser.push('"n2"}\n\n')).toEqual([{ event: 'notification', data: '{"id":"n2"}' }]);
  });

  it('handles CRLF line endings and several events in one chunk', () => {
    const events = new SseParser().push(
      'event: dashboard-changed\r\ndata: \r\n\r\nevent: notifications-read\r\n\r\n',
    );

    expect(events).toEqual([
      { event: 'dashboard-changed', data: '' },
      { event: 'notifications-read', data: '' },
    ]);
  });

  it('joins multi-line data, ignores comments and ids, defaults the type to message', () => {
    const events = new SseParser().push(': keep-alive\nid: 7\ndata: a\ndata: b\n\n');

    expect(events).toEqual([{ event: 'message', data: 'a\nb' }]);
  });

  it('ignores a blank line with no fields', () => {
    expect(new SseParser().push('\n\n')).toEqual([]);
  });
});
