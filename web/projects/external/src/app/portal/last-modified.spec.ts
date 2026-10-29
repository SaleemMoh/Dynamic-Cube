import { saudiTime } from './last-modified';

describe('saudiTime', () => {
  it('writes the moment in Riyadh time with a 12-hour clock', () => {
    expect(saudiTime('2020-12-04T13:13:00Z', 'ar')).toEqual({ date: '04/12/2020', time: '4:13', period: 'م' });
    expect(saudiTime('2020-12-04T05:05:00Z', 'en')).toEqual({ date: '04/12/2020', time: '8:05', period: 'AM' });
  });
});
