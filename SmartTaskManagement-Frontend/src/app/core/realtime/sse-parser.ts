export interface SseEvent {
  event: string;
  data: string;
}

/**
 * Incremental parser for the text/event-stream format: chunks may split lines
 * and events anywhere. An event is dispatched at a blank line when it has a
 * type or data (the API's dashboard-changed and notifications-read carry no data).
 */
export class SseParser {
  private buffer = '';
  private type = '';
  private data: string[] = [];
  private hasData = false;

  push(chunk: string): SseEvent[] {
    this.buffer += chunk;
    const events: SseEvent[] = [];
    let newline = this.buffer.search(/\r\n|\n|\r/);
    while (newline >= 0) {
      // A CR at the very end may be the first half of a CRLF split across chunks: wait for more.
      if (this.buffer[newline] === '\r' && newline === this.buffer.length - 1) {
        break;
      }
      const line = this.buffer.slice(0, newline);
      const length = this.buffer.startsWith('\r\n', newline) ? 2 : 1;
      this.buffer = this.buffer.slice(newline + length);
      const event = this.line(line);
      if (event) {
        events.push(event);
      }
      newline = this.buffer.search(/\r\n|\n|\r/);
    }
    return events;
  }

  private line(line: string): SseEvent | null {
    if (line === '') {
      return this.dispatch();
    }
    if (line.startsWith(':')) {
      return null;
    }
    const colon = line.indexOf(':');
    const field = colon < 0 ? line : line.slice(0, colon);
    let value = colon < 0 ? '' : line.slice(colon + 1);
    if (value.startsWith(' ')) {
      value = value.slice(1);
    }
    if (field === 'event') {
      this.type = value;
    } else if (field === 'data') {
      this.data.push(value);
      this.hasData = true;
    }
    return null;
  }

  private dispatch(): SseEvent | null {
    const event =
      this.type || this.hasData
        ? { event: this.type || 'message', data: this.data.join('\n') }
        : null;
    this.type = '';
    this.data = [];
    this.hasData = false;
    return event;
  }
}
