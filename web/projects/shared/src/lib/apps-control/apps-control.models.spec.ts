import { problemMessages } from './apps-control.models';

describe('problemMessages', () => {
  it('gives field errors and details as they are', () => {
    expect(problemMessages({ error: { errors: { name: ['Required.'] } } })).toEqual(['name: Required.']);
    expect(problemMessages({ error: { status: 409, title: 'Conflict', detail: 'Already exists.' } })).toEqual(['Already exists.']);
  });

  it('adds the trace reference to an unexpected server error', () => {
    const error = { error: { status: 500, title: 'An error occurred while processing your request.', traceId: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01' } };
    expect(problemMessages(error)).toEqual(['An error occurred while processing your request. (Ref: 4bf92f3577b34da6a3ce929d0e0e4736)']);
  });
});
